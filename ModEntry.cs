using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;

namespace ATLuckyPet
{
    internal sealed class ModEntry : Mod
    {
        private ModConfig cfg;

        // --- Luck ---
        private float savedLuck;
        private bool hasSavedLuck;

        // --- Auto pet ---
        private FarmAnimal target;
        private List<Point> path = new();
        private readonly HashSet<long> skipped = new();
        private int repathTimer;
        private int cooldown;

        public override void Entry(IModHelper helper)
        {
            this.cfg = helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += (_, _) => this.hasSavedLuck = false;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.ResetPet();
        }

        // ================= Điều khiển bật/tắt =================
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (e.Button == this.cfg.ToggleLuckKey)
            {
                this.cfg.LuckEnabled = !this.cfg.LuckEnabled;
                if (!this.cfg.LuckEnabled) this.RestoreLuck();
                this.Helper.WriteConfig(this.cfg);
                this.Notify(this.cfg.LuckEnabled ? "AT Lucky Pet: Luck 100% BẬT" : "AT Lucky Pet: Luck 100% TẮT");
            }
            else if (e.Button == this.cfg.ToggleAutoPetKey)
            {
                this.cfg.AutoPetEnabled = !this.cfg.AutoPetEnabled;
                this.ResetPet();
                this.skipped.Clear();
                this.Helper.WriteConfig(this.cfg);
                this.Notify(this.cfg.AutoPetEnabled ? "AT Lucky Pet: Tự vuốt ve vật nuôi BẬT" : "AT Lucky Pet: Tự vuốt ve vật nuôi TẮT");
            }
        }

        private void Notify(string text) => Game1.addHUDMessage(new HUDMessage(text));

        // ================= Vòng lặp chính =================
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            this.ApplyLuck();

            if (this.cfg.AutoPetEnabled)
                this.UpdateAutoPet();
        }

        // ================= Luck =================
        private void ApplyLuck()
        {
            if (!this.cfg.LuckEnabled || !Context.IsMainPlayer) return;

            var luck = Game1.player.team.sharedDailyLuck;
            if (!this.hasSavedLuck)
            {
                this.savedLuck = (float)luck.Value;
                this.hasSavedLuck = true;
            }
            luck.Value = this.cfg.LuckValue;
        }

        private void RestoreLuck()
        {
            if (this.hasSavedLuck && Context.IsMainPlayer)
                Game1.player.team.sharedDailyLuck.Value = this.savedLuck;
        }

        // ================= Tự vuốt ve vật nuôi =================
        private void ResetPet()
        {
            this.target = null;
            this.path.Clear();
            this.cooldown = 0;
        }

        private void UpdateAutoPet()
        {
            if (!Context.IsPlayerFree || Game1.eventUp || Game1.fadeToBlack || Game1.globalFade) return;
            if (this.cooldown > 0) { this.cooldown--; return; }

            Farmer player = Game1.player;
            GameLocation here = player.currentLocation;

            // Chọn mục tiêu
            if (this.target == null || this.target.wasPet.Value || this.target.currentLocation == null)
            {
                this.target = this.PickTarget();
                this.path.Clear();
                if (this.target == null)
                {
                    this.cfg.AutoPetEnabled = false;
                    this.Helper.WriteConfig(this.cfg);
                    this.Notify("AT Lucky Pet: Đã vuốt ve hết vật nuôi!");
                    return;
                }
            }

            GameLocation there = this.target.currentLocation;

            // Khác địa điểm -> đi tới cửa chuồng / warp
            if (there != here)
            {
                this.HandleLocationChange(here, there);
                return;
            }

            // Cùng địa điểm -> đi lại gần con vật
            Point at = this.target.TilePoint;
            int r = this.WalkTo(here, at, adjacent: true);
            if (r == -1) { this.SkipTarget(); return; }
            if (r == 1)
            {
                int dx = at.X - player.TilePoint.X, dy = at.Y - player.TilePoint.Y;
                if (Math.Abs(dx) > Math.Abs(dy)) player.faceDirection(dx > 0 ? 1 : 3);
                else player.faceDirection(dy > 0 ? 2 : 0);

                this.target.pet(player);
                this.target = null;
                this.path.Clear();
                this.cooldown = 20;
            }
        }

        private FarmAnimal PickTarget()
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return null;

            GameLocation here = Game1.player.currentLocation;
            Point me = Game1.player.TilePoint;

            return farm.getAllFarmAnimals()
                .Where(a => !a.wasPet.Value && a.currentLocation != null && !this.skipped.Contains(a.myID.Value))
                .OrderBy(a => a.currentLocation == here ? 0 : 1)
                .ThenBy(a => Math.Abs(a.TilePoint.X - me.X) + Math.Abs(a.TilePoint.Y - me.Y))
                .FirstOrDefault();
        }

        private void SkipTarget()
        {
            if (this.target != null) this.skipped.Add(this.target.myID.Value);
            this.target = null;
            this.path.Clear();
        }

        private void HandleLocationChange(GameLocation here, GameLocation there)
        {
            Building building = Game1.getFarm().buildings.FirstOrDefault(b => b.indoors.Value == there);

            if (there is AnimalHouse && building != null)
            {
                Point door = building.getPointForHumanDoor();
                Point stand = new(door.X, door.Y + 1);

                if (here is Farm)
                {
                    int r = this.WalkTo(here, stand, adjacent: false);
                    if (r == -1) { this.SkipTarget(); return; }
                    if (r == 1 && there.warps.Count > 0)
                    {
                        var w = there.warps[0];
                        Game1.warpFarmer(there.NameOrUniqueName, w.X, w.Y - 1, false);
                        this.AfterWarp();
                    }
                }
                else
                {
                    Game1.warpFarmer("Farm", stand.X, stand.Y, false);
                    this.AfterWarp();
                }
            }
            else if (there is Farm)
            {
                Point at = this.target.TilePoint;
                Game1.warpFarmer("Farm", at.X, at.Y + 1, false);
                this.AfterWarp();
            }
            else
            {
                this.SkipTarget();
            }
        }

        private void AfterWarp()
        {
            this.path.Clear();
            this.cooldown = 60;
        }

        // ================= Di chuyển =================
        /// <summary>1 = đã tới, 0 = đang đi, -1 = không có đường.</summary>
        private int WalkTo(GameLocation loc, Point goal, bool adjacent)
        {
            Point me = Game1.player.TilePoint;
            bool Arrived(Point p) => adjacent ? Manhattan(p, goal) <= 1 : p == goal;

            if (Arrived(me)) return 1;

            if (--this.repathTimer <= 0 || this.path.Count == 0)
            {
                this.path = this.FindPath(loc, me, Arrived);
                this.repathTimer = 45;
                if (this.path.Count == 0) return -1;
            }

            this.Step();
            return 0;
        }

        private void Step()
        {
            if (this.path.Count == 0) return;

            Farmer p = Game1.player;
            Vector2 dest = new(this.path[0].X * 64f, this.path[0].Y * 64f);
            Vector2 diff = dest - p.Position;
            float dist = diff.Length();

            if (Math.Abs(diff.X) > Math.Abs(diff.Y)) p.faceDirection(diff.X > 0 ? 1 : 3);
            else p.faceDirection(diff.Y > 0 ? 2 : 0);

            if (dist <= this.cfg.MoveSpeed)
            {
                p.Position = dest;
                this.path.RemoveAt(0);
            }
            else
            {
                diff.Normalize();
                p.Position += diff * this.cfg.MoveSpeed;
            }
        }

        private static int Manhattan(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private bool Walkable(GameLocation loc, int x, int y)
        {
            if (!loc.isTileOnMap(new Vector2(x, y))) return false;
            Microsoft.Xna.Framework.Rectangle box = new(x * 64 + 8, y * 64 + 8, 48, 48);
            return !loc.isCollidingPosition(box, Game1.viewport, true, 0, false, Game1.player);
        }

        /// <summary>BFS tìm đường (4 hướng). Trả về danh sách ô cần đi qua, không gồm ô xuất phát.</summary>
        private List<Point> FindPath(GameLocation loc, Point start, Func<Point, bool> goal)
        {
            var queue = new Queue<Point>();
            var came = new Dictionary<Point, Point>();
            var seen = new HashSet<Point> { start };
            Point[] dirs = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
            int iter = 0;

            queue.Enqueue(start);
            while (queue.Count > 0 && iter++ < 8000)
            {
                Point cur = queue.Dequeue();
                if (goal(cur))
                {
                    var result = new List<Point>();
                    while (cur != start) { result.Add(cur); cur = came[cur]; }
                    result.Reverse();
                    return result;
                }

                foreach (Point d in dirs)
                {
                    Point n = new(cur.X + d.X, cur.Y + d.Y);
                    if (seen.Contains(n)) continue;
                    seen.Add(n);
                    if (!this.Walkable(loc, n.X, n.Y)) continue;
                    came[n] = cur;
                    queue.Enqueue(n);
                }
            }
            return new List<Point>();
        }
    }
}
