using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CRApi;
using System.Net;

namespace CRApi_Tests {
    class Program {
        static void Main(string[] args) {
            //https://api2.cr-unblocker.com/start_session
            Crunchyroll cr = new Crunchyroll();
            cr.SpoofSessionWithUnblocker();
            var searchRes = cr.Search("Hyouka");
            if (searchRes.Length >= 1) {
                var seasons = cr.GetSeasons(searchRes[0]);
                if (seasons.Length >= 1) {
                    var episodes = cr.GetEpisodes(seasons[0]);
                    foreach (var episode in episodes) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(episode.name + ":");
                        Console.ForegroundColor = ConsoleColor.White;
                        if (episode.premium_only) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Premium only!");
                        } else {
                            var stream = cr.GetStreamInfo(episode);
                            Console.WriteLine("Language: " + stream.audioLang);
                            Console.WriteLine("Sub Language: " + stream.hardsubLang);
                            Console.WriteLine("Stream Quality: " + stream.BestStreamQuality);
                            //Console.WriteLine("Link: " + stream.BestStream);
                        }
                        Console.WriteLine();
                    }
                } else {
                    Console.WriteLine("Could not find any seasons");
                }
            } else {
                Console.WriteLine("Could not find any series.");
            }
            Console.ReadLine();
        }
    }
}
