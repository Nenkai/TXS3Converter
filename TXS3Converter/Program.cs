using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;

using GTTools.Formats;

using CommandLine;
using CommandLine.Text;

namespace GTTools
{
    class Program
    {
        public static string currentFileName;
        public static bool _texConvChecked = false;
        public static bool _texConvExists = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Gran Turismo TXS3 Converter");
            if (args.Length == 1)
            {
                if (Directory.Exists(args[0]))
                {
                    foreach (var file in Directory.GetFiles(args[0], ".img"))
                    {
                        ProcessFile(file);
                        if (!_texConvExists)
                            return;
                    }

                    Console.WriteLine($"Done.");
                    return;
                }
                else if (File.Exists(args[0])) 
                {
                    ProcessFile(args[0]);
                    Console.WriteLine($"Done.");
                    return;
                }
            }

            Parser.Default.ParseArguments<TexConvImgOptions, TexConvTXSOptions>(args)
                .WithParsed<TexConvImgOptions>(TexConvImg)
                .WithParsed<TexConvTXSOptions>(TexConvTXS);
        }

        public static void TexConvImg(TexConvImgOptions option)
        {
            TXS3ImageFormat format = option.Format switch
            {
                "DXT1" => TXS3ImageFormat.DXT1,
                "DXT3" => TXS3ImageFormat.DXT3,
                "DXT5" => TXS3ImageFormat.DXT5,
                "DXT10" => TXS3ImageFormat.DXT10,
            };

            if (format == TXS3ImageFormat.Unknown)
            {
                Console.WriteLine("Wrong format. Expected DXT1/DXT3/DXT5/DXT10.");
                return;
            }


            foreach (var file in option.InputFiles)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"File '{file}' does not exist.");
                    continue;
                }


                TextureSet3 texSet = new TextureSet3();
                texSet.WriteNames = option.WriteNames;
                texSet.LittleEndian = option.LittleEndian;

                if (texSet.AddFromStandardImage(file, format))
                {
                    texSet.ConvertToTXS(Path.ChangeExtension(file, ".img"));
                    Console.WriteLine($"Converted {file} to TXS3.");
                }
                else
                    Console.WriteLine($"Could not process {file}.");

            }
        }

        public static void TexConvTXS(TexConvTXSOptions option)
        {
            foreach (var inputFile in option.InputFiles)
            {
                if (Directory.Exists(inputFile))
                {
                    foreach (var file in Directory.GetFiles(inputFile, ".img"))
                    {
                        ProcessFile(file);
                        if (!_texConvExists)
                            return;
                    }
                }
                else if (File.Exists(inputFile))
                {
                    ProcessFile(inputFile);
                }
            }
        }

        static void ProcessFile(string path)
        {
            currentFileName = Path.GetFileName(path);
            
            string magic = GetFileMagic(path);
            switch (magic)
            {
                case "TXS3":
                case "3SXT":
                    ConvertTXS3Texture(path);
                    break;
            }
        }

        static void ConvertTXS3Texture(string path)
        {
            if (!_texConvChecked && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "texconv.exe")))
            {
                Console.WriteLine("TexConv (image to DDS tool) is missing. Download it from https://github.com/microsoft/DirectXTex/releases and place it next to the tool.");
                return;
            }

            _texConvChecked = true;
            _texConvExists = true;

            var texSet = new TextureSet3();
            texSet.FromFile(path);

            Console.WriteLine($"Found: {texSet.Textures.Count} texture(s)");

            string dir = Path.GetDirectoryName(path);

            for (int i = 0; i < texSet.Textures.Count; i++)
            {
                var texture = texSet.Textures[i];
                Console.WriteLine($"Format: {texture.Format} - {texture.Width}x{texture.Height} ({texture.Mipmap} mipmaps)");

                if (string.IsNullOrEmpty(texture.Name))
                {
                    if (texSet.Textures.Count == 1)
                        texture.Name = Path.GetFileNameWithoutExtension(path);
                    else
                        texture.Name = Path.GetFileNameWithoutExtension(path) + $"_{i}";
                }
                else
                    Console.WriteLine($"Texture Name: {texture.Name}");

                texture.SaveAsPng(dir);
            }

            Console.WriteLine($"Converted {currentFileName} to png.");
        }

        static string GetFileMagic(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);

            Span<byte> mBuf = stackalloc byte[4];
            fs.Read(mBuf);
            return Encoding.ASCII.GetString(mBuf);
        }

        static void ProcessMDL3Model(string path)
        {
            MDL3.FromFile(path);
        }

        [Verb("convertimg", HelpText = "Converts any image to TXS3.")]
        public class TexConvImgOptions
        {
            [Option('i', "input", Required = true, HelpText = "Input standard images.")]
            public IEnumerable<string> InputFiles { get; set; }

            [Option('f', "format", Required = true, HelpText = "Format to convert to. Can be DXT1/3/5/10.")]
            public string Format { get; set; }

            [Option('b', "bundle", HelpText = "Creates a texset/bundle from multiple files (Unimplemented).")]
            public bool Bundle { get; set; }

            [Option('n', "name", HelpText = "Write texture names to the TXS3.")]
            public bool WriteNames { get; set; }

            [Option("le", HelpText = "Write textures as little endian. For GTPSP.")]
            public bool LittleEndian { get; set; }
        }

        [Verb("converttxs", HelpText = "Converts any TXS3 image to standard image.")]
        public class TexConvTXSOptions
        {
            [Option('i', "input", Required = true, HelpText = "Input TXS3 files.")]
            public IEnumerable<string> InputFiles { get; set; }
        }
    }
}
