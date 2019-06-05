using System;
using System.Linq;
using System.Threading.Tasks;

namespace ChiaSeNhac.VN
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //await TestSingleDownload();
            //await TestGetArtistPages();
            await TestGetAlbumPage();
        }

        private static async Task TestGetAlbumPage()
        {
            var url = "https://chiasenhac.vn/playlist/Nhac-Tre-Nhe-Nhang-Sau-Lang~Y3NuX3BsYXlsaXN0fjEwNTM=.html";

            ChiaSeNhacVN downloader = new ChiaSeNhacVN("nhac-tre")
            {
                Threads = 5
            };

            var obj = await downloader.GetAlbumPages(url);

            obj.Print();
            await downloader.DownloadList(obj.Pages.ToList(), null);
        }

        private static async  Task TestGetArtistPages()
        {
            ChiaSeNhacVN downloader = new ChiaSeNhacVN("ngocson");

            var obj = await downloader.GetArtistPages("https://chiasenhac.vn/ca-si/Ngoc-Son~Y3NuX2FydGlzdH4xMDYxMQ==.html");
            //obj.Print();
            await downloader.DownloadList(obj.Pages.ToList());
           // await downloader.DownloadFile(obj.LosslessFile);
        }

        private static async Task TestSingleDownload()
        {
            ChiaSeNhacVN downloader = new ChiaSeNhacVN();

            var html = await downloader.GetHtml("https://vn.chiasenhac.vn/mp3/vietnam/v-pop/nam-lay-tay-anh~tuan-hung~ts35db73qhmqtw.html");

            var obj = downloader.ExtractPlayerConfig(html);
            await downloader.DownloadFile(obj.LosslessFile, 1);
        }
    }
}
