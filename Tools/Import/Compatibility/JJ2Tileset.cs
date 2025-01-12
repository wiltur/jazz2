﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Duality.Drawing;
using Import;

namespace Jazz2.Compatibility
{
    public class JJ2Tileset // .j2t
    {
        private struct TilesetTileSection
        {
            public bool Opaque;
            public uint ImageDataOffset;
            public uint AlphaDataOffset;
            public uint MaskDataOffset;

            public Bitmap Image;
            public Bitmap Mask;
        }

        private string name;
        private JJ2Version version;
        private ColorRgba[] palette;
        private TilesetTileSection[] tiles;
        private int tileCount;

        public string Name => name;

        public int MaxSupportedTiles => (version == JJ2Version.BaseGame ? 1024 : 4096);

        public static JJ2Tileset Open(string path, bool strictParser)
        {
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                // Skip copyright notice
                s.Seek(180, SeekOrigin.Current);

                JJ2Tileset tileset = new JJ2Tileset();

                JJ2Block headerBlock = new JJ2Block(s, 262 - 180);

                uint magic = headerBlock.ReadUInt32();
                if (magic != 0x454C4954 /*TILE*/) {
                    throw new InvalidOperationException("Invalid magic string");
                }

                uint signature = headerBlock.ReadUInt32();
                if (signature != 0xAFBEADDE) {
                    throw new InvalidOperationException("Invalid signature");
                }

                tileset.name = headerBlock.ReadString(32, true);

                ushort versionNum = headerBlock.ReadUInt16();
                tileset.version = (versionNum <= 512 ? JJ2Version.BaseGame : JJ2Version.TSF);

                int recordedSize = headerBlock.ReadInt32();
                if (strictParser && s.Length != recordedSize) {
                    throw new InvalidOperationException("Unexpected file size");
                }

                // Get the CRC; would check here if it matches if we knew what variant it is AND what it applies to
                // Test file across all CRC32 variants + Adler had no matches to the value obtained from the file
                // so either the variant is something else or the CRC is not applied to the whole file but on a part
                int recordedCRC = headerBlock.ReadInt32();

                // Read the lengths, uncompress the blocks and bail if any block could not be uncompressed
                // This could look better without all the copy-paste, but meh.
                int infoBlockPackedSize = headerBlock.ReadInt32();
                int infoBlockUnpackedSize = headerBlock.ReadInt32();
                int imageBlockPackedSize = headerBlock.ReadInt32();
                int imageBlockUnpackedSize = headerBlock.ReadInt32();
                int alphaBlockPackedSize = headerBlock.ReadInt32();
                int alphaBlockUnpackedSize = headerBlock.ReadInt32();
                int maskBlockPackedSize = headerBlock.ReadInt32();
                int maskBlockUnpackedSize = headerBlock.ReadInt32();

                JJ2Block infoBlock = new JJ2Block(s, infoBlockPackedSize, infoBlockUnpackedSize);
                JJ2Block imageBlock = new JJ2Block(s, imageBlockPackedSize, imageBlockUnpackedSize);
                JJ2Block alphaBlock = new JJ2Block(s, alphaBlockPackedSize, alphaBlockUnpackedSize);
                JJ2Block maskBlock = new JJ2Block(s, maskBlockPackedSize, maskBlockUnpackedSize);

                tileset.LoadMetadata(infoBlock);
                tileset.LoadImageData(imageBlock, alphaBlock);
                tileset.LoadMaskData(maskBlock);

                return tileset;
            }
        }

        private void LoadMetadata(JJ2Block block)
        {
            palette = new ColorRgba[256];

            for (int i = 0; i < 256; i++) {
                byte red = block.ReadByte();
                byte green = block.ReadByte();
                byte blue = block.ReadByte();
                byte alpha = block.ReadByte();
                palette[i] = new ColorRgba(red, green, blue, (byte)(255 - alpha));
            }

            tileCount = block.ReadInt32();

            int maxTiles = MaxSupportedTiles;
            tiles = new TilesetTileSection[maxTiles];

            for (int i = 0; i < maxTiles; ++i) {
                tiles[i].Opaque = block.ReadBool();
            }

            // Block of unknown values, skip
            block.DiscardBytes(maxTiles);

            for (int i = 0; i < maxTiles; ++i) {
                tiles[i].ImageDataOffset = block.ReadUInt32();
            }

            // Block of unknown values, skip
            block.DiscardBytes(4 * maxTiles);

            for (int i = 0; i < maxTiles; ++i) {
                tiles[i].AlphaDataOffset = block.ReadUInt32();
            }

            // Block of unknown values, skip
            block.DiscardBytes(4 * maxTiles);

            for (int i = 0; i < maxTiles; ++i) {
                tiles[i].MaskDataOffset = block.ReadUInt32();
            }

            // We don't care about the flipped masks, those are generated on runtime
            block.DiscardBytes(4 * maxTiles);
        }

        private void LoadImageData(JJ2Block imageBlock, JJ2Block alphaBlock)
        {
            const int BlockSize = 32;

            for (int i = 0; i < tiles.Length; i++) {
                ref TilesetTileSection tile = ref tiles[i];
                tile.Image = new Bitmap(BlockSize, BlockSize);

                byte[] imageData = imageBlock.ReadRawBytes(BlockSize * BlockSize, tile.ImageDataOffset);
                byte[] alphaMaskData = alphaBlock.ReadRawBytes(128, tile.AlphaDataOffset);
                for (int j = 0; j < (BlockSize * BlockSize); j++) {
                    byte idx = imageData[j];
                    ColorRgba color;
                    if (alphaMaskData.Length > 0 && ((alphaMaskData[j / 8] >> (j % 8)) & 0x01) == 0x00) {
                        //color = Color.Transparent;
                        color = JJ2DefaultPalette.ByIndex[0];
                    } else {
                        //color = palette[idx];
                        color = JJ2DefaultPalette.ByIndex[idx];
                    }

                    tile.Image.SetPixel(j % BlockSize, j / BlockSize, Color.FromArgb(color.A, color.R, color.G, color.B));
                }
            }
        }

        private void LoadMaskData(JJ2Block block)
        {
            const int BlockSize = 32;

            for (int i = 0; i < tiles.Length; i++) {
                ref TilesetTileSection tile = ref tiles[i];
                tile.Mask = new Bitmap(BlockSize, BlockSize);

                byte[] maskData = block.ReadRawBytes(128, tile.MaskDataOffset);
                for (int j = 0; j < 128; j++) {
                    byte idx = maskData[j];
                    for (int k = 0; k < 8; k++) {
                        int pixelIdx = 8 * j + k;
                        if (((idx >> k) & 0x01) == 0) {
                            tile.Mask.SetPixel(pixelIdx % BlockSize, pixelIdx / BlockSize, Color.Transparent);
                        } else {
                            tile.Mask.SetPixel(pixelIdx % BlockSize, pixelIdx / BlockSize, Color.Black);
                        }
                    }
                }
            }
        }

        public void Convert(string path)
        {
            const int TileSize = 32;
            // Rearrange tiles from '10 tiles per row' to '30 tiles per row'
            const int TilesPerRow = 30;

            // Save tiles and mask
            Bitmap tilesTexture = new Bitmap(TileSize * TilesPerRow, ((tileCount - 1) / TilesPerRow + 1) * TileSize, PixelFormat.Format32bppArgb);
            Bitmap masksTexture = new Bitmap(TileSize * TilesPerRow, ((tileCount - 1) / TilesPerRow + 1) * TileSize, PixelFormat.Format32bppArgb);

            using (Graphics tilesTextureG = Graphics.FromImage(tilesTexture))
            using (Graphics masksTextureG = Graphics.FromImage(masksTexture)) {
                tilesTextureG.Clear(Color.Transparent);
                masksTextureG.Clear(Color.Transparent);

                int maxTiles = MaxSupportedTiles;
                for (int i = 0; i < maxTiles; i++) {
                    ref TilesetTileSection tile = ref tiles[i];

                    tilesTextureG.DrawImage(tile.Image, (i % TilesPerRow) * TileSize, (i / TilesPerRow) * TileSize);
                    masksTextureG.DrawImage(tile.Mask, (i % TilesPerRow) * TileSize, (i / TilesPerRow) * TileSize);
                }
            }

            PngWriter tilesTextureWriter = new PngWriter(tilesTexture);
            PngWriter masksTextureWriter = new PngWriter(masksTexture);

            tilesTextureWriter.Save(Path.Combine(path, "Diffuse.png"));
            masksTextureWriter.Save(Path.Combine(path, "Mask.png"));

            // Create normal map
            PngWriter normalMap = NormalMapGenerator.FromSprite(tilesTextureWriter,
                    new Point(tilesTexture.Width / TileSize, tilesTexture.Height / TileSize),
                    palette);

            normalMap.Save(Path.Combine(path, "Normals.png"));

            // Save tileset palette
            if (palette != null && palette.Length > 1) {
                using (FileStream s = File.Open(Path.Combine(path, "Main.palette"), FileMode.Create, FileAccess.Write))
                using (BinaryWriter w = new BinaryWriter(s)) {
                    w.Write((ushort)palette.Length);
                    w.Write((int)0); // Empty color
                    for (int i = 1; i < palette.Length; i++) {
                        w.Write((byte)palette[i].R);
                        w.Write((byte)palette[i].G);
                        w.Write((byte)palette[i].B);
                        w.Write((byte)palette[i].A);
                    }
                }
            }
        }
    }
}