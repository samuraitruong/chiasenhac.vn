using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace ChiaSeNhac.VN
{
    public class ChiaSeNhacVN
    {
        private Object locker;

        public string Output { get; }
        ConcurrentQueue<HttpClient> httpClients = new ConcurrentQueue<HttpClient>();

        private HttpClient GetClient()
        {
            HttpClient client = null;

            if (httpClients.TryDequeue(out client))
            {
                return client;
            }

            client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip })
            {
               // Timeout = TimeSpan.FromSeconds(150)
            };

            return client;

        }
        public ChiaSeNhacVN(string output = "")
        {
            Output = output;
            locker = new object();
        }
        public async Task<string> GetHtml(string url)
        {
            Console.WriteLine("Get Html Page : " + url);
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
        }
        public async Task DownloadPage(PageItem page)
        {
            try
            {
                var html = await GetHtml(page.Url);
                var config = ExtractPlayerConfig(html);
                if (config == null || config.LosslessFile == null) return;
               
                await DownloadFile(config.LosslessFile);
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR " + ex.Message + ex.StackTrace);
                page.Print();
            }
        }
        public async Task DownloadFile(FileItem file)
        {
            string filename = Path.GetFileName(file.File).UrlDecode();
            var originalName = filename;
            filename = Path.Combine(this.Output, filename);
            if(File.Exists(filename)) {
                Console.WriteLine($"File exist, ignore {filename}");
                return;
            }
            Console.WriteLine($"Downloading : {file.File}");
            //var webClient = new WebClient();
            //var startTime = DateTime.Now;
            //webClient.DownloadProgressChanged += (sender, e) => {
            //    lock (locker)
            //    {
            //        var ts = DateTime.Now - startTime;
            //        var avg = (long)(e.BytesReceived / ts.TotalSeconds);
            //        Console.Write($"{file.Title} - {e.BytesReceived.HumanRead()}/{e.TotalBytesToReceive.HumanRead()} | Avg Speed {avg.HumanRead()}s");
            //        Console.CursorLeft = 0;
            //    }
            //    //e.Print();
            //};

            //await webClient.DownloadFileTaskAsync(new Uri(file.File), filename);

            DownloadFileWithMultipleThread(file.File, originalName, this.Output);

            Console.WriteLine("File download complete");
            await Task.CompletedTask;
        }
        public async Task<Artist> GetAlbumPages(string url)
        {
            string html = await GetHtml(url);
            var artis = ExtractArtist(html);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var last = doc.DocumentNode.SelectNodes("//*[@class='pagination']//a").LastOrDefault();

            Console.WriteLine("Total page {0}", last.InnerText);
            Parallel.ForEach(Enumerable.Range(1, Convert.ToInt32(last.InnerText)), (index) =>
            {
                try
                {
                    var pageHtml = this.GetHtml($"https://chiasenhac.vn/tab_artist?page={index}&artist_id={artis.Id}&tab=music").Result;
                    var items = this.ParseArtistPage(pageHtml);
                    foreach (var item in items)
                    {
                        artis.Pages.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message + ex.StackTrace);
                }

            });
            Console.WriteLine($"Found {artis.Pages.Count} pages");
            return artis;
        }

        public async Task<Artist> GetArtistPages(string url)
        {
            string html = await GetHtml(url);
            var artis = ExtractArtist(html);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var last = doc.DocumentNode.SelectNodes("//*[@class='pagination']//a").LastOrDefault();

            Console.WriteLine("Total page {0}", last.InnerText);
            Parallel.ForEach(Enumerable.Range(1, Convert.ToInt32(last.InnerText)), (index) =>
            {
                try
                {
                    var pageHtml = this.GetHtml($"https://chiasenhac.vn/tab_artist?page={index}&artist_id={artis.Id}&tab=music").Result;
                    var items = this.ParseArtistPage(pageHtml);
                    foreach (var item in items)
                    {
                        artis.Pages.Add(item);
                    }
                } catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message + ex.StackTrace);
                }

            });
            Console.WriteLine($"Found {artis.Pages.Count} pages");
            return artis;
        }

        public async Task DownloadList(List<PageItem> pages)
        {
            if (!Directory.Exists(this.Output))
            {
                Directory.CreateDirectory(this.Output);
            }

            Parallel.ForEach(pages.Where(x => x.Label == "Lossless"), new ParallelOptions()
            {
                MaxDegreeOfParallelism = 1
            }, (item) =>
            {
                this.DownloadPage(item).Wait();

            });
            await Task.CompletedTask;

        }
        #region Parser
        public List<PageItem> ParseArtistPage(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes("//ul[contains(@class, 'list_music')]/li");
            var result = new List<PageItem>();
            foreach (HtmlNode node in nodes)
            {
                var a = node.SelectSingleNode("div//h5/a");
                var pageItem = new PageItem()
                {
                    Name = a.InnerText.Trim(),
                    Url = a.Attributes["href"].Value,
                    Label = node.SelectSingleNode("div/small/span").InnerText.Trim()
                };
                // pageItem.Print();
                result.Add(pageItem);
            }


            return result;
        }

        string DownloadFileWithMultipleThread(string url, string ouputFilename, string folder, List<string> checkFolders = null, int thread = 20, long chunkSize = 102400)
        {
            string filename = Path.GetFileName(HttpUtility.UrlDecode(url));
            if (!string.IsNullOrEmpty(ouputFilename))
            {
                //need validate the url without file name
                filename = ouputFilename;
            }
            string output = Path.Combine(folder, filename);
            string logFile = output + ".log";

            if (File.Exists(output)) return "";
            if (!Directory.Exists(folder))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }

            List<int> downloadedChunks = new List<int>();
            double resumeBytes = 0;

            int numberOfChunks = 0;
            double totalSizeBytes = 0;
            double downloadedBytes = 0;
            double mb = 1024*1024;
            var client = GetClient();

            var request = new HttpRequestMessage(HttpMethod.Head, new Uri(url));

            var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;

            IEnumerable<string> values;
            if (response.Content.Headers.TryGetValues("Content-Length", out values))
            {
                totalSizeBytes = Convert.ToInt64(values.First());

                lock (locker)
                {
                    Console.Clear();
                    var trimmedTitle = filename.Substring(0, Math.Min(Console.WindowWidth - 30, filename.Length));
                    Console.WriteLine($"Downloading {trimmedTitle} | Size {totalSizeBytes / mb:0.00} MB ");
                }
                numberOfChunks = (int)(totalSizeBytes / chunkSize) + (((long)totalSizeBytes % chunkSize != 0) ? 1 : 0);
            }

            httpClients.Enqueue(client);
            ConcurrentBag<int> failedChunks = new ConcurrentBag<int>();

            //Initial empty file 
            var tlocker = new Object();
            string tempFile = output + ".chunks";
            CancellationTokenSource cts = new CancellationTokenSource();

            using (var fs = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                fs.SetLength((long)totalSizeBytes);

                ConcurrentDictionary<int, int> chunks = new ConcurrentDictionary<int, int>();
                for (int i = 1; i <= numberOfChunks; i++)
                {
                    chunks.TryAdd(i, downloadedChunks.Contains(i) ? 2 : 0);
                }
                var list = chunks.Keys.Select(x => chunks[x]).ToList();
                //WriteStatus(list, chunkSize, printLegend: true);
                var startTime = DateTime.Now;

                ThreadPool.SetMinThreads(thread, thread);
                var lResult = Parallel.ForEach(Enumerable.Range(1, numberOfChunks).Where(x => !downloadedChunks.Contains(x)),
                 new ParallelOptions() { MaxDegreeOfParallelism = thread }, (s, state, index) =>
                 {
                     string chunkFileName = output + "." + s;
                     long chunkStart = (s - 1) * chunkSize;
                     long chunkEnd = Math.Min(chunkStart + chunkSize - 1, (long)totalSizeBytes);
                     lock (tlocker)
                     {
                         chunks.TryUpdate(s, 1, 0);
                         list = chunks.Keys.Select(x => chunks[x]).ToList();
                     }

                     var chunkDownload = DownloadChunk(url, chunkStart, chunkEnd, chunkFileName).Result;


                     lock (locker)
                     {
                         downloadedBytes += chunkDownload.Length;
                         var ts = DateTime.Now - startTime;
                         var kbs = (downloadedBytes / 1000) / ts.TotalSeconds;
                         var eta = (totalSizeBytes - downloadedBytes - resumeBytes) / 1024 / kbs;
                         double etaHour = Math.Floor(eta / 3600);
                         double etaMin = Math.Floor((eta - etaHour * 3600) / 60);
                         double etaSec = eta % 60;
                         lock (locker)
                         {
                             Console.SetCursorPosition(0, 1);

                             Console.WriteLine($"#{s:000} | Received: {(downloadedBytes + resumeBytes) / mb:0.00} MB - {(downloadedBytes + resumeBytes) / totalSizeBytes:P2} | Speed : {kbs:0.00} KB/s | ETA: {etaHour:00}:{etaMin:00}:{etaSec:00}");
                         }
                         fs.Seek(chunkStart, SeekOrigin.Begin);
                         fs.Write(chunkDownload, 0, chunkDownload.Length);
                         chunks.TryUpdate(s, 2, 1);
                         list = chunks.Keys.Select(x => chunks[x]).ToList();
                         File.AppendAllText(logFile, "\r\n" + s);
                     }
                 });

                if (!lResult.IsCompleted)
                {
                    cts.Cancel();
                }
            }
            cts.Cancel();

            Console.WriteLine("\r\nFile Download completed.");


            //Task.Run(() =>
            //{
            File.Move(tempFile, output);
            File.Delete(logFile);
            return output;

        }

    private async Task<byte[]> DownloadChunk(string url, long chunkStart, long chunkEnd, string chunkFileName, int retry = 5)
    {
        byte[] data = null;
        var client = GetClient();

        try
        {
            var request = new HttpRequestMessage { RequestUri = new Uri(url) };
            request.Headers.Range = new RangeHeaderValue(chunkStart, chunkEnd);

            var response = await client.SendAsync(request);
            data = await response.Content.ReadAsByteArrayAsync();

            if (data.Length < chunkEnd - chunkStart)
            {
                throw new Exception("Data checked not pass, retrying...");
            }
        }
        catch (Exception ex)
        {
                Console.WriteLine(ex.Message);
            if (retry > 0)
                return await DownloadChunk(url, chunkStart, chunkEnd, chunkFileName, retry - 1);

            throw ex;
        }
        finally
        {
            httpClients.Enqueue(client);
        }

        return data;//chunkFileName;
    }
    public PlayerConfig ExtractPlayerConfig(string html)
        {
            try
            {
                var regex = new Regex("player.setup\\(([^);]*)");
                var match = regex.Match(html);
                if (match != null && match.Groups.Count > 0)
                {
                    string json = match.Groups[1].Value;
                    return JsonConvert.DeserializeObject<PlayerConfig>(json);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR : could not get player config" + ex.Message);
            }
            return null;
        }
        public Artist ExtractArtist(string html)
        {
            var regex = new Regex("'artist_id'\\s:\\s'(\\d*)'");

            var match = regex.Match(html);
            var artist = new Artist();

            if (match != null && match.Groups.Count > 0)
            {
                string id = match.Groups[1].Value;
                artist.Id = id;
            }

            regex = new Regex("'name'\\s?:\\s'(.*)'");
            match = regex.Match(html);

            if (match != null && match.Groups.Count > 0)
            {
                artist.Name= match.Groups[1].Value;
            }

            return artist;
        }
        #endregion

    }
}
