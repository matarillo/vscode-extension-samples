using LanguageServer;
using LanguageServer.Client;
using LanguageServer.Parameters;
using LanguageServer.Parameters.Client;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SampleServer
{
    public class App
    {
        private readonly Connection connection;
        private readonly TextDocumentManager documents;
        private readonly Proxy proxy;
        private bool hasConfigurationCapability = false;
        private bool hasWorkspaceFolderCapability = false;
        private bool hasDiagnosticRelatedInformationCapability = false;

        private readonly Dictionary<Uri, Task<ExampleSettings>> documentSettings;

        private ExampleSettings globalSettings = ExampleSettings.defaultSettings;

        public App(Stream input, Stream output)
        {
            connection = new Connection(input, output);
            proxy = new Proxy(connection);
            documents = new TextDocumentManager();
            documentSettings = new Dictionary<Uri, Task<ExampleSettings>>();
        }

        public Task Listen()
        {
            Logger.Instance.Attach(connection);

            connection.RequestHandlers.Set<RequestMessage<InitializeParams>, ResponseMessage<InitializeResult, ResponseError<InitializeErrorData>>>("initialize", OnInitialize);

            documents.DidClose += Documents_DidClose;
            documents.DidChangeContent += Documents_DidChangeContent;

            // Make the text document manager listen on the connection
            // for open, change and close text document events
            documents.Listen(connection);
            // Listen on the connection
            return connection.Listen();
        }

        private ResponseMessage<InitializeResult, ResponseError<InitializeErrorData>> OnInitialize(RequestMessage<InitializeParams> message, CancellationToken token)
        {
            var capabilities = message.@params.capabilities;

            // Does the client support the `workspace/configuration` request?
            // If not, we will fall back using global settings
            hasConfigurationCapability =
                capabilities?.workspace?.configuration ?? false;
            hasWorkspaceFolderCapability =
                capabilities?.workspace?.workspaceFolders ?? false;
            hasDiagnosticRelatedInformationCapability =
                capabilities?.textDocument?.publishDiagnostics?.relatedInformation ?? false;

            return new ResponseMessage<InitializeResult, ResponseError<InitializeErrorData>>
            {
                result = new InitializeResult
                {
                    capabilities = new ServerCapabilities
                    {
                        textDocumentSync = TextDocumentSyncKind.Full,
                        completionProvider = new CompletionOptions
                        {
                            resolveProvider = true
                        }
                    }
                }
            };
        }

        private void OnInitialized(VoidRequestMessage message)
        {
            if (hasConfigurationCapability)
            {
                proxy.Client.RegisterCapability(new LanguageServer.Parameters.Client.RegistrationParams
                {
                    registrations = new[]
                    {
                        new Registration
                        {
                            id = Guid.NewGuid().ToString(),
                            method = "workspace/didChangeConfiguration"
                        }
                    }
                });
            }

            if (hasWorkspaceFolderCapability)
            {
                connection.NotificationHandlers.Set<NotificationMessage<DidChangeWorkspaceFoldersParams>>("workspace/didChangeWorkspaceFolders", OnDidChangeWorkspaceFolders);
            }
        }

        private void OnDidChangeWorkspaceFolders(NotificationMessage<DidChangeWorkspaceFoldersParams> message)
        {
            Console.Error.WriteLine("Workspace folder change event received.");
        }

        private void OnDidChangeConfiguration(NotificationMessage<DidChangeConfigurationParams> message)
        {
            var change = message.@params;
            if (hasConfigurationCapability)
            {
                // Reset all cached document settings
                documentSettings.Clear();
            }
            else
            {
                globalSettings = ExampleSettings.Create(change?.settings);
            }

            foreach (var doc in documents.All)
            {
                var ignore = ValidateTextDocument(doc);
            }
        }

        private Task<ExampleSettings> GetDocumentSettings(Uri resource)
        {
            if (!hasConfigurationCapability)
            {
                return Task.FromResult(globalSettings);
            }
            var result = documentSettings[resource];
            if (result == null)
            {
                result = GetDocumentSettingsInternal(resource);
                documentSettings[resource] = result;
            }
            return result;
        }

        private async Task<ExampleSettings> GetDocumentSettingsInternal(Uri resource)
        {
            var response = await proxy.Workspace.Configuration(new ConfigurationParams
            {
                items = new[]
                {
                    new ConfigurationItem
                    {
                        scopeUri = resource,
                        section = "languageServerExample"
                    }
                }
            });
            var settings = response.SuccessValue[0];
            return ExampleSettings.Create(settings);
        }

        // Only keep settings for open documents
        private void Documents_DidClose(object sender, TextDocumentChangedEventArgs e)
        {
            documentSettings.Remove(e.Document.Uri);
        }

        // The content of a text document has changed. This event is emitted
        // when the text document first opened or when its content has changed.
        private void Documents_DidChangeContent(object sender, TextDocumentChangedEventArgs e)
        {
            var ignore = ValidateTextDocument(e.Document);
        }

        private async Task ValidateTextDocument(TextDocument textDocument)
        {
            // In this simple example we get the settings for every validate run.
            var settings = await documentSettings[textDocument.Uri];

            // The validator creates diagnostics for all uppercase words length 2 and more
            var text = textDocument.Text;
            var pattern = new Regex("\\b[A-Z]{2,}\\b");

            var diagnostics = new List<Diagnostic>();
            var mc = pattern.Matches(text);
            for (var problems = 0; problems < mc.Count && problems < settings.maxNumberOfProblems; problems++)
            {
                var m = mc[problems];
                var diagnostic = new Diagnostic
                {
                    severity = DiagnosticSeverity.Warning,
                    range = new Range
                    {
                        start = textDocument.PositionAt(m.Index),
                        end = textDocument.PositionAt(m.Index + m.Length)
                    },
                    message = $"{m.Value} is all uppercase.",
                    source = "ex"
                };
                if (hasDiagnosticRelatedInformationCapability)
                {
                    diagnostic.relatedInformation = new[]
                    {
                        new DiagnosticRelatedInformation
                        {
                            location = new Location
                            {
                                uri = textDocument.Uri,
                                range = new Range
                                {
                                    start = diagnostic.range.start,
                                    end = diagnostic.range.end
                                }
                            },
                            message = "Spelling matters"
                        },
                        new DiagnosticRelatedInformation
                        {
                            location = new Location
                            {
                                uri = textDocument.Uri,
                                range = new Range
                                {
                                    start = diagnostic.range.start,
                                    end = diagnostic.range.end
                                }
                            },
                            message = "Particularly for names"
                        }
                    };
                }
                diagnostics.Add(diagnostic);
            }

            // Send the computed diagnostics to VSCode.
            proxy.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                uri = textDocument.Uri,
                diagnostics = diagnostics.ToArray()
            });
        }

        // connection.onDidChangeWatchedFiles

        // This handler provides the initial list of the completion items.
        // connection.onCompletion

        // This handler resolve additional information for the item selected in
        // the completion list.
        // connection.onCompletionResolve

        // https://github.com/Microsoft/vscode-extension-samples/blob/master/lsp-sample/server/src/server.ts
    }
}
