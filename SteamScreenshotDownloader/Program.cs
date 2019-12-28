using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SteamScreenshotDownloader
{
    class SteamImage
    {
        ulong Id { get; set; }

        string Url { get; set; }

        public SteamImage(ulong id, string url)
        {
            Id = id;
            Url = url;
        }

        public void DownloadImage(string filePath)
        {
            string imgPath = Path.Combine(filePath, $"{Id.ToString()}.jpg");

            if (!File.Exists(imgPath))
            {
                using WebClient client = new WebClient();

                string content = client.DownloadString(Url);
                string imageUrl = content.Split("actualmediactn")[1].Split("https://steamuserimages-a.akamaihd.net/ugc/")[1].Split("\"")[0];
                imageUrl = $"https://steamuserimages-a.akamaihd.net/ugc/{imageUrl}";
                client.DownloadFile(imageUrl, imgPath);
            }
        }

        public override string ToString()
            => $"SteamImage: [{Id}] >> {Url}";
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Steam Screenshot Downloader";

            ColorPrint($"Steam Screenshot Downloader ({typeof(Program).Assembly.GetName().Version})");
            ColorPrint($"Insert Steam Account Names (comma seperated): ", ConsoleColor.White, false);

            Console.ForegroundColor = ConsoleColor.Cyan;
            string[] accounts = Console.ReadLine().Split(",", StringSplitOptions.RemoveEmptyEntries);

            foreach (string account in accounts)
            {
                string accountUrl = $"https://steamcommunity.com/id/{account}/screenshots/";
                if (accountUrl.Contains("https://steamcommunity.com/", StringComparison.OrdinalIgnoreCase)
                    && accountUrl.Contains("screenshots", StringComparison.OrdinalIgnoreCase))
                {
                    if (!accountUrl.Contains("view=grid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (accountUrl.Contains("?"))
                        {
                            accountUrl += "&view=grid";
                        }
                        else
                        {
                            accountUrl += "?view=grid";
                        }
                    }

                    DownloadScreenshots(accountUrl);
                }
                else
                {
                    ColorPrint($"Invalid Account! (example, you need to take this account name, not the displayname: https://steamcommunity.com/id/USERNAME/screenshots/)", ConsoleColor.Red);
                }
            }

            ColorPrint($"Press any key to exit...");
            Console.ReadKey();
        }

        private static void DownloadScreenshots(string screenshotsUrl)
        {
            HttpClient httpClient = new HttpClient();

            string username = "";

            int currentPage = 1;
            int nextPage = 1;
            int retryCount = 0;

            List<SteamImage> images = new List<SteamImage>();

            do
            {
                try
                {
                    string url = $"{screenshotsUrl.Trim()}&p={currentPage}";
                    HttpResponseMessage response = httpClient.GetAsync(url).GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        username = content.Split("<title>Steam Community ::")[1].Split(" :: Screenshots</title>")[0].Trim();

                        ColorPrint($"Downloading screenshots from ", ConsoleColor.White, false);
                        ColorPrint($"{username} ", ConsoleColor.Cyan, false, false);
                        ColorPrint($"Page ", ConsoleColor.White, false, false);
                        ColorPrint($"{currentPage}", ConsoleColor.Cyan, true, false);

                        string page = content.Split("pagingCurrentPage")[1];
                        currentPage = int.Parse(page.Split(">")[1].Split("<")[0]);

                        try
                        {
                            string[] paginationParts = content.Split("\"pagingPageLink\"");
                            string splittedPagination = paginationParts[paginationParts.Length - 1].Split(">")[1].Split("<")[0];
                            nextPage = int.Parse(splittedPagination);
                        }
                        catch
                        {
                            nextPage = currentPage;
                        }

                        content = content.Split("<div id=\"image_wall\">")[1];
                        string[] rawImages = content.Split("https://steamcommunity.com/sharedfiles/filedetails/?id=", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

                        foreach (string img in rawImages)
                        {
                            try
                            {
                                string rawId = img.Split("\"")[0];
                                string fullUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={rawId}";

                                images.Add(new SteamImage(ulong.Parse(rawId), fullUrl));
                            }
                            catch (Exception e)
                            {
                                ColorPrint($"Error: ", ConsoleColor.White, false);
                                ColorPrint($"{e.ToString()}", ConsoleColor.Red, true, false);
                                currentPage = int.MaxValue;
                            }
                        }
                    }
                    else
                    {
                        ColorPrint($"unsuccessful... [HTTPCode: {response.StatusCode}]", ConsoleColor.Red, true, false);
                    }

                    currentPage++;
                    retryCount = 0;
                }
                catch
                {
                    retryCount++;
                }
            } while (currentPage - 1 < nextPage && retryCount < 4);

            if (images.Count > 0)
            {
                ColorPrint($"Downloading ", ConsoleColor.White, false);
                ColorPrint($"{images.Count} ", ConsoleColor.Cyan, false, false);
                ColorPrint($"images, this may take a while... ", ConsoleColor.White, true, false);

                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string dataPath = Path.Combine(basePath, "screenshots", username);

                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                Parallel.ForEach(images, img =>
                {
                    img.DownloadImage(dataPath);
                });

                ColorPrint($"Finished ", ConsoleColor.White, false);
                ColorPrint($"successfully", ConsoleColor.Green, true, false);
            }
            else
            {
                ColorPrint($"No images found...", ConsoleColor.Red);
            }
        }

        static void ColorPrint(string msg, ConsoleColor color = ConsoleColor.White, bool endline = true, bool printprefix = true)
        {
            if (printprefix)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(">> ");
            }

            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ResetColor();

            if (endline)
            {
                Console.Write("\n");
            }
        }
    }
}
