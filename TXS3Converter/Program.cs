using System;
using System.Linq;
using System.IO;

namespace TXS3Converter
{
    class Program
    {
        public static bool isBatchConvert = false;
        private static int processedFiles = 0;
        public static string currentFileName;

        static void Main(string[] args)
        {
            Console.WriteLine("TXS3 Converter for Gran Turismo 5/6");
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: TXS3 <input_files>");
            }
            else
            {
                foreach (var arg in args)
                {
                    if (!File.Exists(arg) && !Directory.Exists(arg))
                    {
                        Console.WriteLine($"File does not exist: {arg}");
                        continue;
                    }

                    FileAttributes attr = File.GetAttributes(arg);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        string[] files = Directory.GetFiles(arg, "*.*", SearchOption.TopDirectoryOnly);
                        if (files.Length > 1) isBatchConvert = true;
                        foreach (string f in files)
                        {
                            currentFileName = Path.GetFileName(f);
                            try
                            {
                                TXS3.FromFile(f).SaveAsPng(Path.ChangeExtension(f, ".png"));
                                Console.WriteLine($@"Converted {currentFileName} to png.");
                                processedFiles++;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (args.Length > 1) isBatchConvert = true;
                        currentFileName = Path.GetFileName(arg);
                        try
                        {
                            TXS3.FromFile(arg).SaveAsPng(Path.ChangeExtension(arg, ".png"));
                            Console.WriteLine($@"Converted {currentFileName} to png.");
                            processedFiles++;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}");
                            continue;
                        }
                    }
                }
            }

            Console.WriteLine($"Done, {processedFiles} files were converted. (Press any key to exit)");
            Console.ReadKey();

        }
    }
}
