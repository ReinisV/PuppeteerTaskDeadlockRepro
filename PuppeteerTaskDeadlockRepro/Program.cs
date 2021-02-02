using PuppeteerSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PuppeteerTaskDeadlockRepro
{
    class Program
    {
        static ManualResetEvent quitEvent = new ManualResetEvent(false);
        static ManualResetEvent browserSetEvent = new ManualResetEvent(false);

        static async Task Main(string[] args)
        {
            var browserFetcher = new BrowserFetcher();
            Console.WriteLine("fetching browser");
            await browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision);

            Console.WriteLine("launching browser");
            
            // case #1: browser from current thread will deadlock on the Tasks it returns when the 
            // current thread is blocked by "quitEvent.WaitOne()"
            // in this example, it will happen here: "await browser.NewPageAsync()"
            var browser = await GetBrowserFromCurrentThread();
            
            // case #2: if you create the browser on a different thread (that won't be blocked),
            // the app will work as expected (a new tab will be opened and navigated to google every 5 seconds)
            // var browser = GetBrowserFromAnotherThread();

            Console.CancelKeyPress += (sender, eArgs) =>
            {
                Console.WriteLine("App killed");
                quitEvent.Set();
                eArgs.Cancel = true;
            };

            RunIndefinitely(browser);

            quitEvent.WaitOne();

            // cleanup code here
        }

        private static Browser GetBrowserFromAnotherThread()
        {
            Browser browser = null;
            var thread = new Thread(async () =>
            {
                browser = await Puppeteer.LaunchAsync
                (
                    new LaunchOptions
                    {
                        Headless = false
                    }
                );
                browserSetEvent.Set();


            });

            thread.Start();
            browserSetEvent.WaitOne();

            return browser;
        }

        private static async Task<Browser> GetBrowserFromCurrentThread()
        {
            return await Puppeteer.LaunchAsync
            (
                new LaunchOptions
                {
                    Headless = false
                }
            );
        }

        private static void RunIndefinitely(Browser browser)
        {
            var thread = new Thread(async () =>
            {
                while (true)
                {
                    await PrintPageInfo(browser);
                    Console.WriteLine("loop finished");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            });
            thread.Start();
        }

        private static async Task PrintPageInfo(Browser browser)
        {
            var url = "https://www.google.com";

            Console.WriteLine("opening new page");
            var page = await browser.NewPageAsync();
            Console.WriteLine("starting navigation");
            await page.GoToAsync(url);
            Console.WriteLine("waiting for navigation");
            var contents = await page.GetContentAsync();
            Console.WriteLine($"{url} : {contents.Length}");
        }
    }
}
