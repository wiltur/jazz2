﻿using System;
using Duality;
using Jazz2.Actors;
using Jazz2.Game.Collisions;
using Jazz2.Game.Structs;
using MathF = Duality.MathF;

namespace Jazz2.Game.Tiles
{
    partial class TileMap
    {
        public int CheckWeaponDestructible(ref AABB aabb, WeaponType weapon, int strength)
        {
            int x1 = Math.Max(0, (int)aabb.LowerBound.X >> 5);
            int x2 = Math.Min((int)aabb.UpperBound.X >> 5, levelWidth - 1);
            int y1 = Math.Max(0, (int)aabb.LowerBound.Y >> 5);
            int y2 = Math.Min((int)aabb.UpperBound.Y >> 5, levelHeight - 1);

            int hit = 0;
            for (int tx = x1; tx <= x2; tx++) {
                for (int ty = y1; ty <= y2; ty++) {
                    ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
                    if (tile.DestructType == TileDestructType.Weapon) {
                        if (weapon == WeaponType.Freezer && (animatedTiles[tile.DestructAnimation].Length - 2) > tile.DestructFrameIndex) {
                            FrozenBlock frozen = new FrozenBlock();
                            frozen.OnActivated(new ActorActivationDetails {
                                LevelHandler = levelHandler,
                                Pos = new Vector3(32 * tx + 16 - 1, 32 * ty + 16 - 1, LevelHandler.MainPlaneZ)
                            });
                            levelHandler.AddActor(frozen);
                            hit++;
                        } else if (tile.ExtraData == 0 || tile.ExtraData == (uint)(weapon + 1)) {
                            if (AdvanceDestructibleTileAnimation(ref tile, tx, ty, ref strength, "SceneryDestruct")) {
                                hit++;
                            }

                            if (strength <= 0) {
                                goto Done;
                            }
                        }
                    }
                }
            }
        Done:
            return hit;
        }

        public int CheckSpecialDestructible(ref AABB aabb)
        {
            int x1 = Math.Max(0, (int)aabb.LowerBound.X >> 5);
            int x2 = Math.Min((int)aabb.UpperBound.X >> 5, levelWidth - 1);
            int y1 = Math.Max(0, (int)aabb.LowerBound.Y >> 5);
            int y2 = Math.Min((int)aabb.UpperBound.Y >> 5, levelHeight - 1);

            int hit = 0;
            for (int tx = x1; tx <= x2; tx++) {
                for (int ty = y1; ty <= y2; ty++) {
                    ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
                    if (tile.DestructType == TileDestructType.Special) {
                        int amount = 1;
                        if (AdvanceDestructibleTileAnimation(ref tile, tx, ty, ref amount, "SceneryDestruct")) {
                            hit++;
                        }
                    }
                }
            }
            return hit;
        }

        public int CheckSpecialSpeedDestructible(ref AABB aabb, float speed)
        {
            int x1 = Math.Max(0, (int)aabb.LowerBound.X >> 5);
            int x2 = Math.Min((int)aabb.UpperBound.X >> 5, levelWidth - 1);
            int y1 = Math.Max(0, (int)aabb.LowerBound.Y >> 5);
            int y2 = Math.Min((int)aabb.UpperBound.Y >> 5, levelHeight - 1);

            int hit = 0;
            for (int tx = x1; tx <= x2; tx++) {
                for (int ty = y1; ty <= y2; ty++) {
                    ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
                    if (tile.DestructType == TileDestructType.Speed && /*tile.ExtraData +*/ 5 <= speed) {
                        int amount = 1;
                        if (AdvanceDestructibleTileAnimation(ref tile, tx, ty, ref amount, "SceneryDestruct")) {
                            hit++;
                        }
                    }
                }
            }

            return hit;
        }

        public uint CheckCollapseDestructible(ref AABB aabb)
        {
            int x1 = Math.Max(0, (int)aabb.LowerBound.X >> 5);
            int x2 = Math.Min((int)aabb.UpperBound.X >> 5, levelWidth - 1);
            int y1 = Math.Max(0, (int)aabb.LowerBound.Y >> 5);
            int y2 = Math.Min((int)aabb.UpperBound.Y >> 5, levelHeight - 1);

            uint hit = 0;
            for (int tx = x1; tx <= x2; tx++) {
                for (int ty = y1; ty <= y2; ty++) {
                    ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
                    if (tile.DestructType == TileDestructType.Collapse && !activeCollapsingTiles.Contains(new Point2(tx, ty))) {
                        activeCollapsingTiles.Add(new Point2(tx, ty));
                        hit++;
                    }
                }
            }

            return hit;
        }

        public void AdvanceDestructibleTileAnimationExternally(int tx, int ty, int amount)
        {
            ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
            AdvanceDestructibleTileAnimation(ref tile, tx, ty, ref amount, null);
        }

        public void RevertDestructibleTileAnimationExternally(int tx, int ty)
        {
            ref LayerTile tile = ref layers[sprLayerIndex].Layout[tx + ty * levelWidth];
            if (tile.DestructType == TileDestructType.None || tile.DestructFrameIndex == 0) {
                return;
            }

            tile.DestructFrameIndex = 0;
            tile.TileID = animatedTiles[tile.DestructAnimation].Tiles[tile.DestructFrameIndex].TileID;
            tile.MaterialOffset = tileset.GetTileTextureRect(tile.TileID);

#if MULTIPLAYER && SERVER
            ((LevelHandler)levelHandler).OnRevertDestructibleTileAnimation(tx, ty);
#endif
        }

        private bool AdvanceDestructibleTileAnimation(ref LayerTile tile, int tx, int ty, ref int amount, string soundName)
        {
            int max = (animatedTiles[tile.DestructAnimation].Length - 2);
            if (tile.DestructFrameIndex < max) {
                // Tile not destroyed yet, advance counter by one
                int current = MathF.Min(amount, max - tile.DestructFrameIndex);

                tile.DestructFrameIndex += current;
                tile.TileID = animatedTiles[tile.DestructAnimation].Tiles[tile.DestructFrameIndex].TileID;
                tile.MaterialOffset = tileset.GetTileTextureRect(tile.TileID);
                if (tile.DestructFrameIndex >= max) {
                    if (soundName != null) {
                        levelHandler.PlayCommonSound(soundName, new Vector3(tx * 32 + 16, ty * 32 + 16, LevelHandler.MainPlaneZ));
                    }
                    AnimatedTile anim = animatedTiles[tile.DestructAnimation];
                    CreateTileDebris(ref anim.Tiles[anim.Length - 1], tx, ty);
                }

                amount -= current;

#if MULTIPLAYER && SERVER
                ((LevelHandler)levelHandler).OnAdvanceDestructibleTileAnimation(tx, ty, current);
#endif
                return true;
            }
            return false;
        }

        private void AdvanceCollapsingTileTimers()
        {
            for (int i = 0; i < activeCollapsingTiles.Count; i++) {
                Point2 tilePos = activeCollapsingTiles[i];
                ref LayerTile tile = ref layers[sprLayerIndex].Layout[tilePos.X + tilePos.Y * levelWidth];
                if (tile.ExtraData == 0) {
                    int amount = 1;
                    if (!AdvanceDestructibleTileAnimation(ref tile, tilePos.X, tilePos.Y, ref amount, "SceneryCollapse")) {
                        tile.DestructType = TileDestructType.None;
                        activeCollapsingTiles.RemoveAtFast(i);
                    } else {
                        tile.ExtraData = 4;
                    }
                } else {
                    tile.ExtraData--;
                }
            }
        }
    }
}