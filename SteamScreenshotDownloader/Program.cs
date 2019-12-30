using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
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

        public bool DownloadImage(string filePath)
        {
            string imgPath = Path.Combine(filePath, $"{Id.ToString()}.jpg");

            if (!File.Exists(imgPath))
            {
                try
                {
                    HttpClient client = new HttpClient();
                    HttpResponseMessage response = client.GetAsync(Url).GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        string imageUrl = content.Split("actualmediactn")[1].Split("https://steamuserimages-a.akamaihd.net/ugc/")[1].Split("\"")[0];
                        imageUrl = $"https://steamuserimages-a.akamaihd.net/ugc/{imageUrl}";

                        response = client.GetAsync(imageUrl).GetAwaiter().GetResult();

                        if (response.IsSuccessStatusCode)
                        {
                            using Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                            using FileStream fileStream = new FileInfo(imgPath).OpenWrite();

                            stream.CopyToAsync(fileStream).GetAwaiter().GetResult();
                            return true;
                        }
                    }
                }
                catch { }
            }
            else
            {
                return true;
            }

            if (File.Exists(imgPath))
            {
                File.Delete(imgPath);
            }

            return false;
        }

        public override string ToString()
            => $"SteamImage: [{Id}] >> {Url}";
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Steam Screenshot Downloader";

            ColorPrint($"Steam Screenshot Downloader ", ConsoleColor.White, false);
            ColorPrint($"{typeof(Program).Assembly.GetName().Version}", ConsoleColor.Cyan, true, false);

            string[] accounts;

            if (args.Length >= 1)
            {
                accounts = args[0].Split(",", StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                ColorPrint($"Insert Steam Account Names (comma seperated): ", ConsoleColor.White, false);
                Console.ForegroundColor = ConsoleColor.Cyan;
                accounts = Console.ReadLine().Split(",", StringSplitOptions.RemoveEmptyEntries);
                Console.ResetColor();
            }

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

                    string dataPath;
                    if (args.Length >= 2 && Directory.Exists(args[1]))
                    {
                        dataPath = args[1];
                    }
                    else
                    {
                        string basePath = AppDomain.CurrentDomain.BaseDirectory;
                        dataPath = Path.Combine(basePath, "screenshots", account.Trim());
                    }

                    DownloadScreenshots(accountUrl, dataPath);
                }
                else
                {
                    ColorPrint($"Invalid Account! (example, you need to take this account name, not the displayname: https://steamcommunity.com/id/USERNAME/screenshots/)", ConsoleColor.Red);
                }
            }

            if (args.Length == 0)
            {
                ColorPrint($"Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void DownloadScreenshots(string screenshotsUrl, string dataPath)
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
            } while (currentPage - 1 < nextPage && retryCount < 12);

            if (images.Count > 0)
            {
                ColorPrint($"Downloading ", ConsoleColor.White, false);
                ColorPrint($"{images.Count} ", ConsoleColor.Cyan, false, false);
                ColorPrint($"images, this may take a while...\n", ConsoleColor.White, true, false);

                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                int imageCount = images.Count;
                int finishedImageCount = 0;
                object progressLock = new object();

                Parallel.ForEach(images, img =>
                {
                    while (!img.DownloadImage(dataPath))
                    {
                        Thread.Sleep(1000);
                    };

                    lock (progressLock)
                    {
                        finishedImageCount++;
                        UpdateProgress(imageCount, finishedImageCount);
                    };
                });

                ColorPrint($"Finished ", ConsoleColor.White, false);
                ColorPrint($"successfully", ConsoleColor.Green, true, false);
            }
            else
            {
                ColorPrint($"No images found...", ConsoleColor.Red);
            }
        }

        static void UpdateProgress(int imageCount, int finishedImageCount)
        {
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
            ColorPrint($"Downloaded ", ConsoleColor.White, false);
            ColorPrint($"{finishedImageCount}", ConsoleColor.Cyan, false, false);
            ColorPrint($"/", ConsoleColor.White, false, false);
            ColorPrint($"{imageCount} ", ConsoleColor.Green, false, false);
            ColorPrint($"Images [", ConsoleColor.White, false, false);
            ColorPrint($"{(int)((double)finishedImageCount / (double)imageCount * 100.0)}%", ConsoleColor.Green, false, false);
            ColorPrint($"]", ConsoleColor.White, true, false);
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
