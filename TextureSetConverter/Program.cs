using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using PDTools.Files.Models.PS3.ModelSet3;
using PDTools.Files.Textures;
using PDTools.Files.Textures.PS3;

using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using static PDTools.Files.Textures.TextureSet3;

namespace TextureSetConverter;

public class Program
{
    private readonly static ILoggerFactory _loggerFactory;
    private readonly static ILogger _logger;

    public static Version? GetExecutableVersion() => Assembly.GetEntryAssembly()?.GetName().Version;

    private static bool isBatchConvert = false;
    private static int processedFiles = 0;
    public static string? currentFileName;


    static Program()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog(svc => new NLog.LogFactory()));
        _logger = _loggerFactory.CreateLogger<Program>();
    }

    public static async Task<int> Main(string[] args)
    {
        _logger.LogInformation("Gran Turismo Texture Set (TXS3) / PDI Texture (PDI0) Converter - {:D3}", GetExecutableVersion());
        _logger.LogInformation("by Nenkai | https://github.com/Nenkai");

        var convertPngCommand = new Command("convert-png", "Converts any img/mdl3/strb to png.")
        {
            new Option<string[]>("--input", aliases: ["-i"]) { Required = true, Description = "Input files/textures or folders." },
            new Option<TextureSetPlatformFormatType>("--format", aliases: ["-f"])
            {
                Description = "TXS format. Currently supported is PS3/PS4/PSP. Defaults to PS3. Some textures may not work or output correctly.",
                DefaultValueFactory = (e) => TextureSetPlatformFormatType.PS3
            },
        };
        convertPngCommand.SetAction(ConvertToPng);

        var convertImgCommand = new Command("convert-img", "Converts any image to .img.")
        {
            new Option<string[]>("--input", aliases: ["-i"]) { Required = true, Description = "Input texture files to bake into a texture set file." },
            new Option<string>("--output", aliases: ["-o"]) { Description = "Output texture set file. Required only if building a texture set with more than one texture" },
            new Option<TextureSetPlatformFormatType>("--format", aliases: ["-f"]) { Description = "TXS format. Currently supported is PS3. Valid options: PS3",
                DefaultValueFactory = (e) => TextureSetPlatformFormatType.PS3},
            new Option<string>("--pixel-format", aliases: ["-pf"]) { Description = "Pixel format when converting for PS3 - Valid options: DXT1/DXT3/DXT5/DXT10. Defaults to DXT5.",
                DefaultValueFactory = (e) => "DXT5"},
        };
        convertImgCommand.SetAction(ConvertToImg);

        var rootCommand = new RootCommand()
        {
            convertPngCommand,
            convertImgCommand,
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    public static void ConvertToPng(ParseResult parseResult)
    {
        string[] inputPaths = parseResult.GetValue<string[]>("--input")!;
        TextureSetPlatformFormatType platformFormatType = parseResult.GetValue<TextureSetPlatformFormatType>("--format");

        _logger.LogInformation("Platform Type: {} (change this with --format)", platformFormatType);

        foreach (var file in inputPaths)
        {
            if (!File.Exists(file) && !Directory.Exists(file))
            {
                _logger.LogError("File does not exist: {}", file);
                continue;
            }

            FileAttributes attr = File.GetAttributes(file);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                string[] files = Directory.GetFiles(file, "*.*", SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    currentFileName = Path.GetFileName(f);
                    try
                    {
                        ConvertFileToPng(f, platformFormatType);
                        processedFiles++;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, @"Could not convert {}", currentFileName);
                    }
                }
            }
            else
            {
                currentFileName = Path.GetFileName(file);
                try
                {
                    ConvertFileToPng(file, platformFormatType);
                    processedFiles++;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, @"Could not convert {}", currentFileName);
                }
            }
        }

        _logger.LogInformation("Done, {} files were converted.", processedFiles);
    }

    public static void ConvertToImg(ParseResult parseResult)
    {
        string[] inputPaths = parseResult.GetValue<string[]>("--input")!;
        string? outputPath = parseResult.GetValue<string>("--output");
        TextureSetPlatformFormatType platformFormatType = parseResult.GetValue<TextureSetPlatformFormatType>("--format");
        string pixelFormat = parseResult.GetValue<string>("--pixel-format")!;

        _logger.LogInformation("Platform Type: {} (--format)", platformFormatType);
        _logger.LogInformation("Pixel Format: {} (--pixel-format)", pixelFormat);

        TextureSet3 textureSet = new TextureSet3(_loggerFactory);

        foreach (var file in inputPaths)
        {
            if (!AddFileToTextureSet(textureSet, file, platformFormatType, pixelFormat))
            {
                _logger.LogError("Failed to add file '{}', aborting.", file);
                return;
            }
        }

        _logger.LogInformation("Building texture set with {} texture(s).", textureSet.TextureInfos.Count);

        if (inputPaths.Length == 1)
        {
            textureSet.BuildTextureSetFile(Path.ChangeExtension(inputPaths.First(), ".img"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                _logger.LogError("Please specify an output file name when building multiple textures into one texture set.");
                return;
            }

            _logger.LogWarning("Building a texture set file with multiple textures is not yet supported.");
            return;
        }
    }

    static bool _texConvExists = false;
    static void ConvertFileToPng(string path, TextureSetPlatformFormatType consoleType)
    {
        currentFileName = Path.GetFileName(path);

        string magic = GetFileMagic(path);
        switch (magic)
        {
            case "TXS3":
            case "3SXT":
                ProcessTextureSetToPng(path, consoleType);
                return;
            case "STRB":
                ProcessStrobeFile(path);
                return;
            case "IDP0":
                ConvertPDITextureToPng(path);
                return;
            case "MDL3":
            case "3LDM":
                ProcessModelSetFile(path);
                return;
        }
    }

    public static void ProcessStrobeFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);
        fs.Position = 0xB0;
        int offset = fs.ReadInt32();
        int count = fs.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            fs.Position = offset + (i * 0x10);
            int textureOffset = fs.ReadInt32();

            fs.Position = textureOffset;

            var texture = new TextureSet3();
            texture.FromStream(fs, TextureSetPlatformFormatType.PS3);
            texture.ConvertToStandardFormat(path + $"_{i}.png");
        }
    }

    public static bool AddFileToTextureSet(TextureSet3 textureSet, string path, TextureSetPlatformFormatType platformFormatType,
        string cellFormat)
    {
        if (platformFormatType != TextureSetPlatformFormatType.PS3)
        {
            _logger.LogError("Currently only PS3 TXS3 can be created.");
            return false;
        }

        // Convert if valid extension
        string ext = Path.GetExtension(path);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            if (!_texConvExists && !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "texconv.exe")))
            {
                _logger.LogError("TexConv (image to DDS tool) is missing. Download texconv.exe from https://github.com/microsoft/DirectXTex/releases and place it next to the executable.");
                return false;
            }
            _texConvExists = true;

            CELL_GCM_TEXTURE_FORMAT format = 0;
            if (cellFormat.Equals("DXT1"))
                format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT1;
            else if (cellFormat.Equals("DXT3"))
                format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT23;
            else if (cellFormat.Equals("DXT5"))
                format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT45;
            else if (cellFormat.Equals("DXT10"))
                format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_A8R8G8B8;
            else
            {
                _logger.LogError("ERROR: DXT format is invalid or not provided. must be DXT1/DXT3/DXT5/DXT10.");
                return false;
            }

            var texture = new PGLUCellTextureInfo();
            texture.FormatBits = format;

            /*
            if (verbs.Swizzle)
                (texture.TextureRenderInfo as PGLUCellTextureInfo).FormatBits &= ~CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_LN; // Remove the default linear flag, set up swizzle
            */

            var added = texture.FromStandardImage(path, format);
            if (!added)
                return false;

            textureSet.AddTexture(texture);

            return true;
        }
        else
        {
            _logger.LogWarning("Skipped {}, no operation to be done", path);
            return true;
        }
    }

    static void ProcessTextureSetToPng(string path, TextureSetPlatformFormatType consoleType)
    {
        _logger.LogInformation("Converting {} to png.", currentFileName);

        var txs = new TextureSet3();
        txs.FromFile(path, consoleType);
        txs.ConvertToStandardFormat(Path.ChangeExtension(path, ".png"));
    }

    static void ConvertPDITextureToPng(string path)
    {
        var pdiTexture = new PDITexture();
        pdiTexture.FromFile(path);
        pdiTexture.TextureSet.ConvertToStandardFormat(Path.ChangeExtension(path, ".png"));

        _logger.LogInformation("Converted {} to png.", currentFileName);
    }

    static void ProcessModelSetFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);
        using var bs = new BinaryStream(fs);
        ModelSet3 set = ModelSet3.FromStream(bs);

        set.TextureSet.ConvertToStandardFormat(path);
    }

    static string GetFileMagic(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);

        Span<byte> mBuf = stackalloc byte[4];
        fs.ReadExactly(mBuf);
        return Encoding.ASCII.GetString(mBuf);
    }
}