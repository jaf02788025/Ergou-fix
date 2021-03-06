﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;

namespace KaiHelper
{
    public class JungleTimer
    {
        private readonly List<JungleCamp> _jungleCamps = new List<JungleCamp>();
        private readonly Font _miniMapFont;
        private readonly Font _mapFont;
        private int _nextTime;

        public JungleTimer(Menu config)
        {
            _menuJungle = config;
            _menuJungle.AddItem(new MenuItem("JungleActive", "打野 计时").SetValue(true));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Blue", 300, new Vector3(3871.489f, 7901.054f, 51.90324f),
                    new[] { "SRU_Blue1.1.1", "SRU_BlueMini1.1.2", "SRU_BlueMini21.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Murkwolf", 100, new Vector3(3780.628f, 6443.984f, 52.4632f),
                    new[] { "SRU_Murkwolf2.1.1", "SRU_MurkwolfMini2.1.2", "SRU_MurkwolfMini2.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Razorbeak", 100, new Vector3(6823.895f, 5457.756f, 53.12784f),
                    new[]
                    {
                        "SRU_Razorbeak3.1.1", "SRU_RazorbeakMini3.1.2", "SRU_RazorbeakMini3.1.3", "SRU_RazorbeakMini3.1.4"
                    }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Red", 300, new Vector3(7862f, 4112f, 53.71951f),
                    new[] { "SRU_Red4.1.1", "SRU_RedMini4.1.2", "SRU_RedMini4.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Krug", 100, new Vector3(8532.471f, 2737.948f, 50.58278f),
                    new[] { "SRU_Krug5.1.2", "SRU_KrugMini5.1.1" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Dragon", 360, new Vector3(9866.148f, 4414.014f, -71.2406f), new[] { "SRU_Dragon6.1.1" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Blue", 300, new Vector3(10931.73f, 6990.844f, 51.72291f),
                    new[] { "SRU_Blue7.1.1", "SRU_BlueMini7.1.2", "SRU_BlueMini27.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Murkwolf", 100, new Vector3(11008f, 8386f, 62.13136f),
                    new[] { "SRU_Murkwolf8.1.1", "SRU_MurkwolfMini8.1.2", "SRU_MurkwolfMini8.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Razorbeak", 100, new Vector3(7986.997f, 9471.389f, 52.34794f),
                    new[]
                    {
                        "SRU_Razorbeak9.1.1", "SRU_RazorbeakMini9.1.2", "SRU_RazorbeakMini9.1.3", "SRU_RazorbeakMini9.1.4"
                    }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Red", 300, new Vector3(7016.869f, 10775.55f, 56.00922f),
                    new[] { "SRU_Red10.1.1", "SRU_RedMini10.1.2", "SRU_RedMini10.1.3" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Krug", 100, new Vector3(6317.092f, 12146.46f, 56.4768f),
                    new[] { "SRU_Krug11.1.2", "SRU_KrugMini11.1.1" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Baron", 420, new Vector3(5007.124f, 10471.45f, -71.2406f), new[] { "SRU_Baron12.1.1" }));
            _jungleCamps.Add(
                new JungleCamp(
                    "SRU_Gromp", 100, new Vector3(2090.628f, 8427.984f, 51.77725f), new[] { "SRU_Gromp13.1.1" }));
            _jungleCamps.Add(
                new JungleCamp("SRU_Gromp", 100, new Vector3(12702f, 6444f, 51.69143f), new[] { "SRU_Gromp14.1.1" }));
            _jungleCamps.Add(
                new JungleCamp("Sru_Crab", 180, new Vector3(10500f, 5170f, -62.8102f), new[] { "Sru_Crab15.1.1" }));
            _jungleCamps.Add(
                new JungleCamp("Sru_Crab", 180, new Vector3(4400f, 9600f, -66.53082f), new[] { "Sru_Crab16.1.1" }));
            _mapFont = new Font(Drawing.Direct3DDevice, new System.Drawing.Font("Times New Roman", 20));
            _miniMapFont = new Font(Drawing.Direct3DDevice, new System.Drawing.Font("Times New Roman", 8));
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnEndScene += Drawing_OnEndScene;
        }

        private void Drawing_OnEndScene(EventArgs args)
        {
            if (!IsActive())
            {
                return;
            }
            foreach (JungleCamp jungleCamp in _jungleCamps.Where(camp => camp.NextRespawnTime > 0))
            {
                string timeClock =
                    (jungleCamp.NextRespawnTime - (int) Game.ClockTime).ToString(CultureInfo.InvariantCulture);
                Vector2 pos = Drawing.WorldToMinimap(jungleCamp.Position);
                Helper.DrawText(_miniMapFont, timeClock, (int) pos.X, (int) pos.Y - 8, Color.White);
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (!IsActive())
            {
                return;
            }
            foreach (JungleCamp jungleCamp in _jungleCamps.Where(camp => camp.NextRespawnTime > 0))
            {
                string timeClock =
                    (jungleCamp.NextRespawnTime - (int) Game.ClockTime).ToString(CultureInfo.InvariantCulture);
                Vector2 pos = Drawing.WorldToScreen(jungleCamp.Position);
                Helper.DrawText(_mapFont, timeClock, (int) pos.X, (int) pos.Y - 15, Color.White);
            }
        }
        private void Game_OnGameUpdate(EventArgs args)
        {
            if (!IsActive())
            {
                return;
            }
            if ((int) Game.ClockTime - _nextTime >= 0)
            {
                _nextTime = (int) Game.ClockTime + 1;
                var minions =
                    ObjectManager.Get<Obj_AI_Base>()
                        .Where(minion => !minion.IsDead && minion.IsValid && minion.Name.ToUpper().StartsWith("SRU"));

                var junglesAlive =
                    _jungleCamps.Where(
                        jungle =>
                            !jungle.IsDead &&
                            jungle.Names.Any(
                                s =>
                                    minions.Where(minion => minion.Name == s)
                                        .Select(minion => minion.Name)
                                        .FirstOrDefault() != null));
                foreach (var jungle in junglesAlive)
                {
                    jungle.Visibled = true;
                }
                var junglesDead =
                    _jungleCamps.Where(
                        jungle =>
                            !jungle.IsDead && jungle.Visibled &&
                            jungle.Names.All(
                                s =>
                                    minions.Where(minion => minion.Name == s)
                                        .Select(minion => minion.Name)
                                        .FirstOrDefault() == null));
                foreach (var jungle in junglesDead)
                {
                    jungle.IsDead = true;
                    jungle.Visibled = false;
                    jungle.NextRespawnTime = (int) Game.ClockTime + jungle.RespawnTime;
                }
                foreach (JungleCamp jungleCamp in
                    _jungleCamps.Where(jungleCamp => (jungleCamp.NextRespawnTime - (int) Game.ClockTime) <= 0))
                {
                    jungleCamp.IsDead = false;
                    jungleCamp.NextRespawnTime = 0;
                }
            }
        }

        public class JungleCamp
        {
            public String Name;
            public int NextRespawnTime;
            public int RespawnTime;
            public bool IsDead;
            public bool Visibled;
            public Vector3 Position;
            public string[] Names;

            public JungleCamp(String name, int respawnTime, Vector3 position, string[] names)
            {
                Name = name;
                RespawnTime = respawnTime;
                Position = position;
                Names = names;
                IsDead = false;
                Visibled = false;
            }
        }

        private bool IsActive()
        {
            return _menuJungle.Item("JungleActive").GetValue<bool>();
        }

        private readonly Menu _menuJungle;
    }
}