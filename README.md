# TextureSetConverter
An utility to convert TextureSet3 (TXS3) files to standard PNG, rewritten from TXS2DDS.

Current source has support for, but unfinished:
* GT5/6 (TXS3/MDL3/STRB)
* GT7 (PDI0)
* GTPSP (TXS3)

# Downloading
Download in the "Releases" tab.

# How to use

* Into .png: `convert-png <input_file(s)> <format i.e PS3/PS4/PS5>`
* Into .img: `convert-img <input_file(s)> PS3 -pf <DXT1/DXT3/DXT5/DXT10>`
