using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace ImageSearchApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // validate args
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: ImageSearch <largeImage> <smallImage> <nThreads> <algorithm>");
                return;
            }

            string largePath = args[0];
            string smallPath = args[1];
            if (!int.TryParse(args[2], out int nThreads) || nThreads < 1)
            {
                Console.WriteLine("Invalid number of threads. Must be >= 1.");
                return;
            }
            string algo = args[3].ToLower();
            if (algo != "exact" && algo != "euclidian")
            {
                Console.WriteLine("Invalid algorithm. Use 'exact' or 'euclidian'.");
                return;
            }

            // Load images (try)
            Bitmap largeImg, smallImg;
            try
            {
                largeImg = new Bitmap(largePath);
                smallImg = new Bitmap(smallPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading images: {ex.Message}");
                return;
            }
            // if the dims of the small one are bigger than the large one, no possiable match
            int bigW = largeImg.Width;
            int bigH = largeImg.Height;
            int smallW = smallImg.Width;
            int smallH = smallImg.Height;
            if (smallW > bigW || smallH > bigH)
                return;
            int maxX = bigW - smallW;
            int maxY = bigH - smallH;

            // Convert to Color arrays
            Color[][] bigColors = ToColorArray(largeImg);
            Color[][] smallColors = ToColorArray(smallImg);
            
            // key structures
            var results = new List<Point>(); // list of points
            object lockObj = new object(); // lock object, to prevent race conditions
            var threads = new List<Thread>(); // list of all threads, will use later to join all
            
            // get row per thread
            int rowsPerThread = (maxY + 1 + nThreads - 1) / nThreads;
            for (int i = 0; i < nThreads; i++)
            {
                int startY = i * rowsPerThread;
                int endY = Math.Min(maxY, startY + rowsPerThread - 1);
                if (startY > endY) break;
                // we will use lambda so the threads can access outer scope var like bigColors and smallColors
                Thread t = new Thread(() =>
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = 0; x <= maxX; x++)
                        {
                            // fancy way to apply the right algorithm
                            bool match = algo == "exact"
                                ? MatchExact(bigColors, smallColors, x, y)
                                : MatchEuclidean(bigColors, smallColors, x, y);
                            if (match)
                            {
                                lock (lockObj)
                                {
                                    results.Add(new Point(x, y));
                                }
                            }
                        }
                    }
                });
                t.Start();
                threads.Add(t);
            }
            // Wait and join all threads
            foreach (var t in threads)
                t.Join();
            // results , nothing will be if no match
            foreach (var pt in results)
                Console.WriteLine($"{pt.Y},{pt.X}");
        }
        // Bitmap to 2D Color array
        private static Color[][] ToColorArray(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var arr = new Color[h][];
            for (int y = 0; y < h; y++)
            {
                arr[y] = new Color[w];
                for (int x = 0; x < w; x++)
                    arr[y][x] = bmp.GetPixel(x, y);
            }
            return arr;
        }
        // Exact
        private static bool MatchExact(Color[][] big, Color[][] small, int startX, int startY)
        {
            int h = small.Length;
            int w = small[0].Length;
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    if (big[startY + dy][startX + dx] != small[dy][dx])
                        return false;
                }
            }
            return true;
        }
        // Euclidean
        private static bool MatchEuclidean(Color[][] big, Color[][] small, int startX, int startY)
        {
            int h = small.Length;
            int w = small[0].Length;
            double sumDist = 0;
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    var c1 = big[startY + dy][startX + dx];
                    var c2 = small[dy][dx];
                    double dr = c1.R - c2.R;
                    double dg = c1.G - c2.G;
                    double db = c1.B - c2.B;
                    sumDist += Math.Sqrt(dr * dr + dg * dg + db * db);
                    if (sumDist > 0)
                        return false; // images are different
                }
            }
            // match if total distance is zero
            return sumDist == 0;
            // i will say we could consider adding a threshold because most images can differ by extreamly small amount and 
            // by this algo still be considerd different. however the question wanted 0 sum so that what we did.
        }
    }
}
