﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DedicatedServerTool.Avalonia.Core
{
    internal static class InstalledWorkshopModUtility
    {
        internal static string GetModName(string serverInstallDirectory, long workshopId)
        {
            var workshopItemDirectory = Path.Combine(serverInstallDirectory, @$"steamapps\workshop\content\736590\{workshopId}");
            if (Directory.Exists(workshopItemDirectory))
            {
                var modFolderName = Directory.GetDirectories(workshopItemDirectory).FirstOrDefault();
                return Path.GetFileName(modFolderName) ?? string.Empty;
            }
            return string.Empty;
        }

        internal static DateTime GetModLastUpdated(string serverInstallDirectory, long workshopId)
        {
            var workshopItemDirectory = Path.Combine(serverInstallDirectory, @$"steamapps\workshop\content\736590\{workshopId}");
            if (Directory.Exists(workshopItemDirectory))
            {
                var modFolderName = Directory.GetDirectories(workshopItemDirectory).FirstOrDefault();
                if (modFolderName != null)
                {
                    return new FileInfo(modFolderName).LastWriteTime;
                }
                else
                {
                    throw new Exception($"Mod: {workshopId} is not installed or couldn't be found");
                }
            }
            return DateTime.Now;
        }
        internal static IEnumerable<long> GetInstalledWorkshopIds(string serverInstallDirectory)
        {
            var workshopDirectory = Path.Combine(serverInstallDirectory, @"steamapps\workshop\content\736590\");
            if (!Directory.Exists(workshopDirectory))
            {
                return Enumerable.Empty<long>();
            }

            var workshopItemDirectories = Directory.GetDirectories(workshopDirectory)
                .Where(directory => Directory.GetDirectories(directory).Any());
            var workshopIds = new List<long>();
            foreach (var directory in workshopItemDirectories)
            {
                var workshopItemFolderName = Path.GetFileName(directory); // the folder name should just be the workshop item id
                if (long.TryParse(workshopItemFolderName, out var workshopItemId))
                {
                    workshopIds.Add(workshopItemId);
                }
            }
            return workshopIds;
        }

        internal static async Task<bool> IsModOutOfDate(string serverInstallDirectory, long workshopId)
        {

            try
            {
                DateTime workshopLastUpdated = await ScrapeWorkshopItemLastUpdated(workshopId);
                DateTime modFolderLastUpdated = GetModLastUpdated(serverInstallDirectory, workshopId);

                // returns true if workshopLastUpdated is a LATER date than modFolderLastUpdated
                return workshopLastUpdated.CompareTo(modFolderLastUpdated) > 0;
            } catch (Exception ex)
            {
                Console.WriteLine(ex);
                if (ex.Message.Contains("Mod:"))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void OpenWorkshopPage(long workshopId)
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}",
                    UseShellExecute = true,
                }
            );
        }

        internal static async Task<string> GetCoverImagePathAsync(string serverProfileInstallDirectory, long workshopItemId)
        {
            var imagePath = Path.Combine(serverProfileInstallDirectory, @$"steamapps\workshop\content\736590\{workshopItemId}\coverimage.jpg");

            // Check if the image file already exists in the workshop directory
            if (File.Exists(imagePath))
            {
                return imagePath; // Return the path to the existing file
            }

            // If the file does not exist, attempt to download and save it
            var downloadedImagePath = await DownloadWorkshopCoverImageAsync(serverProfileInstallDirectory, workshopItemId);

            // If imagePath is not empty, either the image was downloaded or it already exists
            if (!string.IsNullOrEmpty(downloadedImagePath))
            {
                return downloadedImagePath;
            }

            Console.WriteLine("Failed to retrieve or download the workshop cover image.");
            return string.Empty;
        }

        private static async Task<string> DownloadWorkshopCoverImageAsync(string serverProfileInstallDirectory, long workshopItemId)
        {
            var coverImageUrl = await ScrapeWorkshopItemCoverImageUrl(workshopItemId);
            if (string.IsNullOrWhiteSpace(coverImageUrl))
            {
                return string.Empty;
            }

            var workshopItemDirectory = Path.Combine(serverProfileInstallDirectory, @$"steamapps\workshop\content\736590\{workshopItemId}");

            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(workshopItemDirectory);

                using (var httpClient = new HttpClient())
                {
                    // Download the image content
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(coverImageUrl);

                    // Save the image to the specified directory
                    string imagePath = Path.Combine(workshopItemDirectory, "coverimage.jpg");
                    await File.WriteAllBytesAsync(imagePath, imageBytes);

                    return imagePath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading or saving the image: {ex.Message}");
                return string.Empty;
            }
        }

        private static async Task<string> ScrapeWorkshopItemCoverImageUrl(long workshopItemId)
        {
            string workshopItemUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopItemId}";

            using (var httpClient = new HttpClient())
            {
                // Download the HTML content of the workshop item page
                string htmlContent = await httpClient.GetStringAsync(workshopItemUrl);

                // Load the HTML content into HtmlAgilityPack's HtmlDocument
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(htmlContent);

                // Use XPath to select the cover image element
                var imageNode = htmlDocument.DocumentNode.SelectSingleNode("//img[@id='previewImageMain']");

                if (imageNode != null)
                {
                    // Extract the src attribute value
                    string imageUrl = imageNode.GetAttributeValue("src", "");

                    // The src attribute may contain special characters (e.g., &amp;), so decode it
                    imageUrl = System.Web.HttpUtility.HtmlDecode(imageUrl);

                    return imageUrl;
                }
                else
                {
                    Console.WriteLine("Preview image element not found on the page.");
                    return string.Empty;
                }
            }
        }

        
        public static async Task<DateTime> ScrapeWorkshopItemLastUpdated(long workshopItemId)
        {
            string workshopItemUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopItemId}";

            using (var httpClient = new HttpClient())
            {
                // Download the HTML content of the workshop item page
                string htmlContent = await httpClient.GetStringAsync(workshopItemUrl);
                //htmlContent.cookies



                // GET TIMEZONE OFFSET
                HttpResponseMessage response = await httpClient.GetAsync(workshopItemUrl);

                if (response.IsSuccessStatusCode)
                {
                    var cookies = response.Headers.GetValues("Set-Cookie");

                    foreach (var cookie in cookies)
                    {
                        Console.WriteLine(cookie);
                    }
                }

                // Load the HTML content into HtmlAgilityPack's HtmlDocument
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(htmlContent);


                // Use XPath to select the last updated or date createde(if it hasn't been updated)
                var lastUpdateNode = htmlDocument.DocumentNode.SelectNodes("//div[@class='detailsStatRight']");

                if (lastUpdateNode != null)
                {
                    try
                    {
                        string lastUpdatedString = lastUpdateNode.Last().InnerText.Trim().ToString();
                        return DateTimeUtility.ParseScrapedSteamDateTime(lastUpdatedString);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Failed to scrape workshop item last updated");
                        throw ex;
                    }
                }
                else
                {
                    Console.WriteLine("Date time element not found on the page.");
                    throw new Exception("Date time unavailable on workshop item");
                }
            }
        }
        
    }
}
