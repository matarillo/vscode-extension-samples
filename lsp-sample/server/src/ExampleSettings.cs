namespace SampleServer
{
    public class ExampleSettings
    {
        public static readonly ExampleSettings defaultSettings = new ExampleSettings(1000);

        public ExampleSettings(int maxNumberOfProblems)
        {
            this.maxNumberOfProblems = maxNumberOfProblems;
        }

        public int maxNumberOfProblems { get; private set; }

        public static ExampleSettings Create(dynamic settings)
        {
            var maxNumberOfProblemsSent = settings?.languageServerExample?.maxNumberOfProblems;
            return
                (maxNumberOfProblemsSent != null)
                    ? new ExampleSettings((int)maxNumberOfProblemsSent)
                    : defaultSettings;
        }
    }
}
