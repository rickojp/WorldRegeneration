using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;
using TShockAPI.DB;
using OTAPI.Tile;
using Microsoft.Xna.Framework;

namespace WorldRegeneration
{
    public static class Utilities
    {
        const int BUFFER_SIZE = 1048576;
        public static void SaveWorldSection(int x, int y, int x2, int y2, string path)
        {
            // GZipStream is already buffered, but it's much faster to have a 1 MB buffer.
            using (var writer =
                new BinaryWriter(
                    new BufferedStream(
                        new GZipStream(File.Open(path, FileMode.Create), CompressionMode.Compress), BUFFER_SIZE)))
            {
                writer.Write(Main.worldSurface);
                writer.Write(Main.rockLayer);
                writer.Write(Main.dungeonX);
                writer.Write(Main.dungeonY);
                writer.Write(WorldGen.crimson);

                writer.Write(WorldGen.SavedOreTiers.Copper);
                writer.Write(WorldGen.SavedOreTiers.Silver);
                writer.Write(WorldGen.SavedOreTiers.Iron);
                writer.Write(WorldGen.SavedOreTiers.Gold);

                writer.Write(WorldGen.SavedOreTiers.Cobalt);
                writer.Write(WorldGen.SavedOreTiers.Mythril);
                writer.Write(WorldGen.SavedOreTiers.Adamantite);

                writer.Write(x);
                writer.Write(y);
                writer.Write(x2);
                writer.Write(y2);

                for (int i = x; i <= x2; i++)
                {
                    for (int j = y; j <= y2; j++)
                    {
                        writer.Write(Main.tile[i, j] ?? new Tile());
                    }
                }
                TSPlayer.All.SendInfoMessage("Tile Data Saved...");

                #region Chest Data
                int totalChests = 0;
                for (int i = 0; i < Main.maxChests; i++)
                {
                    Chest chest = Main.chest[i];
                    if (chest != null)
                    {
                        totalChests++;
                    }
                }

                writer.Write(totalChests);
                for (int i = 0; i < Main.maxChests; i++)
                {
                    Chest chest = Main.chest[i];
                    if (chest != null)
                        writer.WriteChest(chest);
                }
                TSPlayer.All.SendInfoMessage("{0} Chest Data Saved...", totalChests);
                #endregion

                #region Sign Data
                int totalSigns = 0;
                for (int i = 0; i < Sign.maxSigns; i++)
                {
                    Sign sign = Main.sign[i];
                    if (sign != null)
                    {
                        totalSigns++;
                    }
                }

                writer.Write(totalSigns);
                for (int i = 0; i < Sign.maxSigns; i++)
                {
                    Sign sign = Main.sign[i];
                    if (sign != null && sign.text != null)
                        writer.WriteSign(sign);
                }
                TSPlayer.All.SendInfoMessage("{0} Sign Data Saved...", totalSigns);
                #endregion

                #region Tile Entitity Data
                writer.Write(TileEntity.ByID.Count);
                foreach (KeyValuePair<int, TileEntity> byID in TileEntity.ByID)
                {
                    TileEntity.Write(writer, byID.Value);
                }
                TSPlayer.All.SendInfoMessage("{0} Tile Entitity Data Saved...", Terraria.DataStructures.TileEntity.ByID.Count);
                #endregion
            }
        }

        public static void Write(this BinaryWriter writer, ITile tile)
        {
            writer.Write(tile.sTileHeader);
            writer.Write(tile.bTileHeader);
            writer.Write(tile.bTileHeader2);

            if (tile.active())
            {
                writer.Write(tile.type);
                if (Main.tileFrameImportant[tile.type])
                {
                    writer.Write(tile.frameX);
                    writer.Write(tile.frameY);
                }
            }
            writer.Write(tile.wall);
            writer.Write(tile.liquid);
        }

        public static void WriteChest(this BinaryWriter writer, Chest chest)
        {
            writer.Write(chest.x);
            writer.Write(chest.y);
            /*
                Chest c = Main.chest[Chest.FindChest(point[0].X, point[0].Y)];
                Main.NewText(string.Concat("Chest Style: ", Main.tile[c.x, c.y].frameX / 2 / 18));
            */
            //writer.Write(chest.name);
            for (int l = 0; l < Chest.maxItems; l++)
            {
                Item item = chest.item[l];
                if (item != null && item.stack > 0)
                {
                    writer.Write((short)item.stack);
                    writer.Write(item.type);
                    writer.Write(item.prefix);
                }
                else
                {
                    writer.Write((short)0);
                }
            }
        }

        public static void WriteSign(this BinaryWriter writer, Sign sign)
        {
            writer.Write(sign.text);
            writer.Write(sign.x);
            writer.Write(sign.y);
        }

        public static void LoadWorldSection(string path, Rectangle rect, bool useRect = false)
        {
            Task.Factory.StartNew(() =>
            {
                using (var reader = new BinaryReader(new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress)))
                {
                    if (WorldRegeneration.Config.UseInfiniteChests)
                    {
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/rconvchests");
                        System.Threading.Thread.Sleep(5000);
                    }
                    Main.worldSurface = reader.ReadDouble();
                    Main.rockLayer = reader.ReadDouble();
                    Main.dungeonX = reader.ReadInt32();
                    Main.dungeonY = reader.ReadInt32();
                    WorldGen.crimson = reader.ReadBoolean();

                    WorldGen.SavedOreTiers.Copper = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Silver = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Iron = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Gold = reader.ReadInt32();

                    WorldGen.SavedOreTiers.Cobalt = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Mythril = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Adamantite = reader.ReadInt32();

                    reader.ReadInt32();
                    reader.ReadInt32();

                    int x = 0;
                    int y = 0;

                    int x2 = reader.ReadInt32();
                    int y2 = reader.ReadInt32();

                    for (int i = x; i <= x2; i++)
                    {
                        for (int j = y; j <= y2; j++)
                        {
                            Tile tile = reader.ReadTile();
                            if (i >= 0 && j >= 0 && i < Main.maxTilesX && j < Main.maxTilesY)
                            {

                                if (TShock.Regions.InAreaRegion(i, j).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    continue;
                                }
                                else if (useRect)
                                    if (rect.Contains(i, j))
                                    {
                                        if (tile.type == 21)
                                            WorldGen.PlaceChest(i, j, 21, false, (tile.frameX / 2) / 18);
                                        else
                                            Main.tile[i, j] = tile;
                                    }
                                    else
                                        continue;
                                else
                                {
                                    if (tile.type == 21)
                                        WorldGen.PlaceChest(i, j, 21, false, (tile.frameX / 2) / 18);
                                    else
                                        Main.tile[i, j] = tile; // Paste Tiles
                                }
                            }
                        }
                    }
                    ResetSection(x, y, x2, y2);
                    TSPlayer.All.SendInfoMessage("Tile Data Loaded...");

                    if (useRect)
                        return;

                    #region Chest Data
                    int totalChests = reader.ReadInt32();
                    int chests = 0;
                    int index = 0;
                    if (!WorldRegeneration.Config.IgnoreChests)
                    {
                        for (int a = 0; a < totalChests; a++)
                        {
                            Chest chest = reader.ReadChest();
                            for (int c = index; c < Main.maxChests; c++)
                            {
                                if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    break;
                                }
                                else if (Main.chest[c] != null && TShock.Regions.InAreaRegion(Main.chest[c].x, Main.chest[c].y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    index++;
                                    continue;
                                }
                                else
                                {
                                    Main.chest[Chest.FindChest(chest.x, chest.y)] = chest;
                                    index++;
                                    chests++;
                                    break;
                                }
                            }
                        }
                        TSPlayer.All.SendInfoMessage("{0} of {1} Chest Data Loaded...", chests, totalChests);
                    }
                    else
                    {
                        for (int a = 0; a < totalChests; a++)
                        {
                            reader.ReadChest();
                        }
                        TSPlayer.All.SendInfoMessage("{0} Chest Data Ignored...", totalChests);
                    }
                    #endregion

                    #region Sign Data
                    int totalSigns = reader.ReadInt32();
                    int signs = 0;
                    index = 0;
                    for (int a = 0; a < totalSigns; a++)
                    {
                        Sign sign = reader.ReadSign();
                        for (int s = index; s < Sign.maxSigns; s++)
                        {
                            if (TShock.Regions.InAreaRegion(sign.x, sign.y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                            {
                                break;
                            }
                            else if (Main.sign[s] != null && TShock.Regions.InAreaRegion(Main.sign[s].x, Main.sign[s].y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                            {
                                index++;
                                continue;
                            }
                            else
                            {
                                Main.sign[s] = sign;
                                index++;
                                signs++;
                                break;
                            }
                        }
                    }
                    TSPlayer.All.SendInfoMessage("{0} of {1} Signs Data Loaded...", signs, totalSigns);
                    #endregion

                    #region Tile Entitity Data
                    int totalTileEntities = reader.ReadInt32();
                    int num1 = 0;
                    for (int i = 0; i < totalTileEntities; i++)
                    {
                        TileEntity tileEntity = TileEntity.Read(reader);
                        for (int j = 0; j < TileEntity.MaxEntitiesPerChunk; j++)
                        {
                            TileEntity entityUsed;
                            if (TileEntity.ByID.TryGetValue(j, out entityUsed))
                            {
                                if (entityUsed.Position == tileEntity.Position)
                                {
                                    break;
                                }
                                continue;
                            }
                            else
                            {
                                tileEntity.ID = j;
                                TileEntity.ByID[tileEntity.ID] = tileEntity;
                                TileEntity.ByPosition[tileEntity.Position] = tileEntity;
                                TileEntity.TileEntitiesNextID = j++;
                                num1++;
                                break;
                            }
                        }
                    }
                    TSPlayer.All.SendInfoMessage("{0} of {1} Tile Entity Data Loaded...", num1, totalTileEntities);

                    if (WorldRegeneration.Config.UseInfiniteChests)
                    {
                        TSPlayer.All.SendInfoMessage("Using InfiniteChests Commands...");
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/convchests");
                        System.Threading.Thread.Sleep(5000);
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/prunechests,");
                    }

                    TSPlayer.All.SendInfoMessage("Successfully regenerated the world.");

                    #endregion
                }
            });
        }
        public static Tile ReadTile(this BinaryReader reader)
        {
            var tile = new Tile
            {
                sTileHeader = reader.ReadInt16(),
                bTileHeader = reader.ReadByte(),
                bTileHeader2 = reader.ReadByte()
            };

            // Tile type
            if (tile.active())
            {
                tile.type = reader.ReadUInt16();
                if (Main.tileFrameImportant[tile.type])
                {
                    tile.frameX = reader.ReadInt16();
                    tile.frameY = reader.ReadInt16();
                }
            }
            tile.wall = reader.ReadUInt16();
            tile.liquid = reader.ReadByte();
            return tile;
        }
        public static Chest ReadChest(this BinaryReader reader)
        {
            Chest chest = new Chest(false)
            {
                x = reader.ReadInt32(),
                y = reader.ReadInt32(),
                name = string.Empty
            };
            for (int l = 0; l < Chest.maxItems; l++)
            {
                Item item = new Item();
                int stack = reader.ReadInt16();
                if (stack > 0)
                {
                    int type = reader.ReadInt32();
                    byte prefix = reader.ReadByte();
                    item.SetDefaults(type);
                    item.stack = stack;
                    item.Prefix(prefix);
                }
                chest.item[l] = item;
            }
            return chest;
        }
        public static Sign ReadSign(this BinaryReader reader)
        {
            Sign sign = new Sign
            {
                text = reader.ReadString(),
                x = reader.ReadInt32(),
                y = reader.ReadInt32()
            };
            return sign;
        }
        public static void ResetSection(int x, int y, int x2, int y2)
        {
            int lowX = Netplay.GetSectionX(x);
            int highX = Netplay.GetSectionX(x2);
            int lowY = Netplay.GetSectionY(y);
            int highY = Netplay.GetSectionY(y2);
            foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
            {
                for (int i = lowX; i <= highX; i++)
                {
                    for (int j = lowY; j <= highY; j++)
                        sock.TileSections[i, j] = false;
                }
            }
        }
        public static void RegenerateWorld(string path)
        {
            Task.Factory.StartNew(() =>
            {
                using (var reader = new BinaryReader(new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress)))
                {
                    if (WorldRegeneration.Config.UseInfiniteChests)
                    {
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/rconvchests");
                        System.Threading.Thread.Sleep(5000);
                    }

                    #region Reset Specific WorldGen Data
                    WorldGen.SavedOreTiers.Cobalt = -1;
                    WorldGen.SavedOreTiers.Mythril = -1;
                    WorldGen.SavedOreTiers.Adamantite = -1;
                    #endregion

                    Main.worldSurface = reader.ReadDouble();
                    Main.rockLayer = reader.ReadDouble();
                    Main.dungeonX = reader.ReadInt32();
                    Main.dungeonY = reader.ReadInt32();
                    WorldGen.crimson = reader.ReadBoolean();

                    WorldGen.SavedOreTiers.Copper = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Silver = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Iron = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Gold = reader.ReadInt32();

                    WorldGen.SavedOreTiers.Cobalt = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Mythril = reader.ReadInt32();
                    WorldGen.SavedOreTiers.Adamantite = reader.ReadInt32();

                    reader.ReadInt32();
                    reader.ReadInt32();

                    int x = 0;
                    int y = 0;

                    int x2 = reader.ReadInt32();
                    int y2 = reader.ReadInt32();

                    for (int i = x; i <= x2; i++)
                    {
                        for (int j = y; j <= y2; j++)
                        {
                            Tile tile = reader.ReadTile();
                            if (i >= 0 && j >= 0 && i < Main.maxTilesX && j < Main.maxTilesY)
                            {
                                if (TShock.Regions.InAreaRegion(i, j).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    continue;
                                }
                                else
                                {
                                    if (tile.type == 21)
                                        WorldGen.PlaceChest(i, j, 21, false, (tile.frameX / 2) / 18);
                                    else
                                        Main.tile[i, j] = tile;
                                }
                            }
                        }
                    }
                    ResetSection(x, y, x2, y2);

                    #region Chest Data
                    int totalChests = reader.ReadInt32();
                    int chests = 0;
                    int index = 0;
                    if (!WorldRegeneration.Config.IgnoreChests)
                    {
                        for (int a = 0; a < totalChests; a++)
                        {
                            Chest chest = reader.ReadChest();
                            for (int c = index; c < Main.maxChests; c++)
                            {
                                if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    break;
                                }
                                else if (Main.chest[c] != null && TShock.Regions.InAreaRegion(Main.chest[c].x, Main.chest[c].y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                                {
                                    index++;
                                    continue;
                                }
                                else
                                {
                                    Main.chest[Chest.FindChest(chest.x, chest.y)] = chest;
                                    index++;
                                    chests++;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int a = 0; a < totalChests; a++)
                        {
                            reader.ReadChest();
                        }
                    }
                    #endregion

                    #region Sign Data
                    int totalSigns = reader.ReadInt32();
                    int signs = 0;
                    index = 0;
                    for (int a = 0; a < totalSigns; a++)
                    {
                        Sign sign = reader.ReadSign();
                        for (int s = index; s < Sign.maxSigns; s++)
                        {
                            if (TShock.Regions.InAreaRegion(sign.x, sign.y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                            {
                                break;
                            }
                            else if (Main.sign[s] != null && TShock.Regions.InAreaRegion(Main.sign[s].x, Main.sign[s].y).Any(r => r != null && r.Z >= WorldRegeneration.Config.MaxZRegion))
                            {
                                index++;
                                continue;
                            }
                            else
                            {
                                Main.sign[s] = sign;
                                index++;
                                signs++;
                                break;
                            }
                        }
                    }
                    #endregion

                    #region Tile Entitity Data
                    int totalTileEntities = reader.ReadInt32();

                    for (int i = 0; i < totalTileEntities; i++)
                    {
                        TileEntity tileEntity = TileEntity.Read(reader);
                        for (int j = 0; j < TileEntity.MaxEntitiesPerChunk; j++)
                        {
                            TileEntity entityUsed;
                            if (TileEntity.ByID.TryGetValue(j, out entityUsed))
                            {
                                if (entityUsed.Position == tileEntity.Position)
                                {
                                    break;
                                }
                                continue;
                            }
                            else
                            {
                                tileEntity.ID = j;
                                TileEntity.ByID[tileEntity.ID] = tileEntity;
                                TileEntity.ByPosition[tileEntity.Position] = tileEntity;
                                break;
                            }
                        }
                    }
                    #endregion

                    TSPlayer.All.SendMessage(string.Format("The world has regenerated..."), 50, 255, 130);

                    #region WorldGen Reset Data
                    if (WorldRegeneration.Config.ResetWorldGenStatus)
                    {
                        Main.hardMode = false;
                        NPC.downedBoss1 = false;
                        NPC.downedBoss2 = false;
                        NPC.downedBoss3 = false;
                        NPC.downedQueenBee = false;
                        NPC.downedSlimeKing = false;
                        NPC.downedMechBossAny = false;
                        NPC.downedMechBoss1 = false;
                        NPC.downedMechBoss2 = false;
                        NPC.downedMechBoss3 = false;
                        NPC.downedFishron = false;
                        NPC.downedMartians = false;
                        NPC.downedAncientCultist = false;
                        NPC.downedMoonlord = false;
                        NPC.downedHalloweenKing = false;
                        NPC.downedHalloweenTree = false;
                        NPC.downedChristmasIceQueen = false;
                        NPC.downedChristmasSantank = false;
                        NPC.downedChristmasTree = false;
                        NPC.downedPlantBoss = false;
                        NPC.savedStylist = false;
                        NPC.savedGoblin = false;
                        NPC.savedWizard = false;
                        NPC.savedMech = false;
                        NPC.downedGoblins = false;
                        NPC.downedClown = false;
                        NPC.downedFrost = false;
                        NPC.downedPirates = false;
                        NPC.savedAngler = false;
                        NPC.downedMartians = false;
                        NPC.downedGolemBoss = false;
                        NPC.savedTaxCollector = false;
                        WorldGen.shadowOrbSmashed = false;
                        WorldGen.altarCount = 0;
                        WorldGen.shadowOrbCount = 0;
                    }
                    #endregion

                    if (WorldRegeneration.Config.UseInfiniteChests)
                    {
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/convchests");
                        System.Threading.Thread.Sleep(10000);
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/prunechests");
                    }
                }
            });
        }
    }
}
