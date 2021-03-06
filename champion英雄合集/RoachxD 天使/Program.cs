﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace Kayle
{
    internal class Program
    {
        public const string ChampionName = "Kayle";
        public static Orbwalking.Orbwalker Orbwalker;
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static SpellSlot IgniteSlot;
        public static Items.Item Dfg;
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        public static Menu Config;

        private static bool RighteousFuryActive
        {
            get { return ObjectManager.Player.AttackRange > 125f; }
        }

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.ChampionName != ChampionName) return;

            Q = new Spell(SpellSlot.Q, 650f);
            W = new Spell(SpellSlot.W, 900f);
            E = new Spell(SpellSlot.E, 625f);
            R = new Spell(SpellSlot.R, 900f);

            IgniteSlot = Player.GetSpellSlot("summonerdot");

            Dfg = Utility.Map.GetMap()._MapType == Utility.Map.MapType.TwistedTreeline
                ? new Items.Item(3188, 750)
                : new Items.Item(3128, 750);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            Config = new Menu(ChampionName, ChampionName, true);

            Config.AddSubMenu(new Menu("走砍", "Orbwalking"));

            var tsMenu = new Menu("目标 选择", "Target Selector");
            TargetSelector.AddToMenu(tsMenu);
            Config.AddSubMenu(tsMenu);

            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            Config.AddSubMenu(new Menu("连招", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQC", "使用 Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseWC", "使用 W").SetValue(false));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseEC", "使用 E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseIgniteC", "使用 引燃").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboActive", "连招!").SetValue(
                new KeyBind(Config.Item("Orbwalk").GetValue<KeyBind>().Key, KeyBindType.Press)));

            Config.AddSubMenu(new Menu("骚扰", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQH", "使用 Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseWH", "使用 W").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEH", "使用 E").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassActive", "骚扰!").SetValue(
                new KeyBind(Config.Item("Farm").GetValue<KeyBind>().Key, KeyBindType.Press)));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassActiveT", "骚扰 (自动)!").SetValue(
                new KeyBind("T".ToCharArray()[0], KeyBindType.Toggle)));

            Config.AddSubMenu(new Menu("清线", "Farm"));
            Config.SubMenu("Farm").AddItem(new MenuItem("UseQF", "使用 Q").SetValue(
                new StringList(new[] {"控线", "清线", "同时", "禁止"}, 1)));
            Config.SubMenu("Farm").AddItem(new MenuItem("UseEF", "使用 E").SetValue(
                new StringList(new[] {"控线", "清线", "同时", "禁止"}, 2)));
            Config.SubMenu("Farm").AddItem(new MenuItem("FreezeActive", "控线!").SetValue(
                new KeyBind(Config.Item("Farm").GetValue<KeyBind>().Key, KeyBindType.Press)));
            Config.SubMenu("Farm").AddItem(new MenuItem("LaneClearActive", "清线!").SetValue(
                new KeyBind(Config.Item("LaneClear").GetValue<KeyBind>().Key, KeyBindType.Press)));

            Config.AddSubMenu(new Menu("清野", "JungleFarm"));
            Config.SubMenu("JungleFarm").AddItem(new MenuItem("UseQJ", "使用 Q").SetValue(true));
            Config.SubMenu("JungleFarm").AddItem(new MenuItem("UseEJ", "使用 E").SetValue(true));
            Config.SubMenu("JungleFarm").AddItem(new MenuItem("JungleFarmActive", "清野!").SetValue(
                new KeyBind(Config.Item("LaneClear").GetValue<KeyBind>().Key, KeyBindType.Press)));

            Config.AddSubMenu(new Menu("大招", "Ultimate"));
            Config.SubMenu("Ultimate").AddSubMenu(new Menu("队友", "Allies"));
            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>()
                .Where(ally => ally.IsAlly))
                Config.SubMenu("Ultimate")
                    .SubMenu("Allies")
                    .AddItem(new MenuItem("Ult" + ally.ChampionName, ally.ChampionName)
                        .SetValue(ally.ChampionName == Player.ChampionName));
            Config.SubMenu("Ultimate")
                .AddItem(new MenuItem("UltMinHP", "R最低生命值").SetValue(new Slider(20, 1)));

            Config.AddSubMenu(new Menu("回复", "Heal"));
            Config.SubMenu("Heal").AddSubMenu(new Menu("队友", "Allies"));
            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>()
                .Where(ally => ally.IsAlly))
            {
                Config.SubMenu("Heal")
                    .SubMenu("Allies")
                    .AddItem(new MenuItem("Heal" + ally.ChampionName, ally.ChampionName)
                        .SetValue(ally.ChampionName == Player.ChampionName));
            }
            Config.SubMenu("Heal")
                .AddItem(new MenuItem("HealMinHP", "回复最低生命值").SetValue(new Slider(40, 1)));


            Config.AddSubMenu(new Menu("杂项", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("UsePackets", "使用 封包").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("SupportMode", "辅助 模式").SetValue(false));

            var comboDmg = new MenuItem("ComboDamage", "显示连招后伤害").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Utility.HpBarDamageIndicator.Enabled = comboDmg.GetValue<bool>();
            comboDmg.ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
                };

            Config.AddSubMenu(new Menu("Drawings", "范围"));
            Config.SubMenu("Drawings").AddItem(new MenuItem("QRange", "Q 范围").SetValue(
                new Circle(false, Color.FromArgb(100, 255, 0, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("WRange", "W 范围").SetValue(
                new Circle(true, Color.FromArgb(100, 255, 0, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("ERange", "E 范围").SetValue(
                new Circle(true, Color.FromArgb(100, 255, 0, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("RRange", "R 范围").SetValue(
                new Circle(false, Color.FromArgb(100, 255, 0, 255))));
            Config.SubMenu("Drawings").AddItem(comboDmg);

            Config.AddToMainMenu();

            Game.PrintChat("RoachxD 澶╀娇涓ㄥ姞杞芥垚鍔燂紒姹夊寲by Bbyyyy锛丵Q缇や辅361630847");

            Game.OnGameUpdate += Game_OnGameUpdate;
            Game.OnGameSendPacket += Game_OnGameSendPacket;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Combo()
        {
            var qTarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            var wTarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            var eTarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            var iTarget = TargetSelector.GetTarget(600, TargetSelector.DamageType.True);

            if (qTarget == null && wTarget == null && eTarget == null && iTarget == null)
            {
                return;
            }

            if (Config.Item("UseQC").GetValue<bool>() && Q.IsReady() && qTarget != null)
            {
                Q.Cast(qTarget, Config.Item("UsePackets").GetValue<bool>());
            }

            if (Config.Item("UseWC").GetValue<bool>() && W.IsReady() && wTarget != null &&
                !Orbwalking.InAutoAttackRange(wTarget))
            {
                W.Cast(Player, Config.Item("UsePackets").GetValue<bool>());
            }

            if (Config.Item("UseEC").GetValue<bool>() && E.IsReady() && eTarget != null &&
                Player.Distance(eTarget) <= E.Range)
            {
                E.Cast();
            }

            if (iTarget != null && Config.Item("UseIgniteC").GetValue<bool>() && IgniteSlot != SpellSlot.Unknown &&
                Player.Spellbook.CanUseSpell(IgniteSlot) == SpellState.Ready)
            {
                if (Player.GetSummonerSpellDamage(iTarget, Damage.SummonerSpell.Ignite) > iTarget.Health)
                {
                    Player.Spellbook.CastSpell(IgniteSlot, iTarget);
                }
            }

            foreach (var minion in ObjectManager.Get<Obj_AI_Minion>()
                .Where(
                    minion =>
                        minion.Distance(eTarget) <= 150 && eTarget != null && RighteousFuryActive &&
                        !Config.Item("SupportMode").GetValue<bool>()))
            {
                Orbwalker.ForceTarget(minion);
            }
        }

        private static void Harass()
        {
            var qTarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            var wTarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            var eTarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);

            if (qTarget == null && eTarget == null)
            {
                return;
            }

            if (Config.Item("UseQH").GetValue<bool>() && Q.IsReady() && qTarget != null)
            {
                Q.Cast(qTarget, Config.Item("UsePackets").GetValue<bool>());
            }

            if (Config.Item("UseWH").GetValue<bool>() && W.IsReady() && wTarget != null &&
                !Orbwalking.InAutoAttackRange(wTarget))
            {
                W.Cast(Player, Config.Item("UsePackets").GetValue<bool>());
            }

            if (Config.Item("UseEH").GetValue<bool>() && E.IsReady() && eTarget != null &&
                Player.Distance(eTarget) <= E.Range)
            {
                E.Cast();
            }

            foreach (var minion in ObjectManager.Get<Obj_AI_Minion>()
                .Where(
                    minion =>
                        minion.Distance(eTarget) <= 150 && eTarget != null && RighteousFuryActive &&
                        !Config.Item("SupportMode").GetValue<bool>()))
            {
                Orbwalker.ForceTarget(minion);
            }
        }

        private static void Farm(bool laneClear)
        {
            var allMinionsQ = MinionManager.GetMinions(Player.ServerPosition, Q.Range);
            var allMinionsE = MinionManager.GetMinions(Player.ServerPosition, E.Range + 150);

            var useQi = Config.Item("UseQF").GetValue<StringList>().SelectedIndex;
            var useEi = Config.Item("UseEF").GetValue<StringList>().SelectedIndex;
            var useQ = (laneClear && (useQi == 1 || useQi == 2)) || (!laneClear && (useQi == 0 || useQi == 2));
            var useE = (laneClear && (useEi == 1 || useEi == 2)) || (!laneClear && (useEi == 0 || useEi == 2));

            if (useQ && Q.IsReady())
            {
                foreach (var minion in allMinionsQ.Where(minion => !Orbwalking.InAutoAttackRange(minion) &&
                                                                   minion.Health <
                                                                   Player.GetSpellDamage(minion, SpellSlot.Q)))
                {
                    Q.Cast(minion, Config.Item("UsePackets").GetValue<bool>());
                }
            }
            if (!useE || !E.IsReady())
            {
                return;
            }

            if (laneClear)
            {
                foreach (var minion in allMinionsE.Where(minion => Player.Distance(minion) <= E.Range))
                {
                    E.Cast();
                }

                foreach (var minion in allMinionsE
                    .Where(
                        eMinion => Player.Distance(eMinion) > E.Range && Player.Distance(eMinion) <= E.Range + 150)
                    .SelectMany(eMinion => ObjectManager.Get<Obj_AI_Minion>()
                        .Where(minion => eMinion.Distance(minion) <= 150 && eMinion != minion)))
                {
                    Orbwalker.ForceTarget(minion);
                }
            }
            else
            {
                foreach (var minion in allMinionsE
                    .Where(
                        minion =>
                            !Orbwalking.InAutoAttackRange(minion) &&
                            !RighteousFuryActive))
                {
                    E.Cast();
                }
            }
        }

        private static void JungleFarm()
        {
            var useQ = Config.Item("UseQJ").GetValue<bool>();
            var useE = Config.Item("UseEJ").GetValue<bool>();

            var mobs = MinionManager.GetMinions(Player.ServerPosition, W.Range, MinionTypes.All,
                MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
            if (mobs.Count <= 0)
            {
                return;
            }

            var mob = mobs[0];
            if (useQ && Q.IsReady())
            {
                Q.Cast(mob, Config.Item("UsePackets").GetValue<bool>());
            }

            if (useE && E.IsReady())
            {
                E.Cast();
            }
        }

        private static void Ultimate()
        {
            foreach (var ally in from ally in ObjectManager.Get<Obj_AI_Hero>()
                .Where(ally => ally.IsAlly && !ally.IsDead && Utility.CountEnemysInRange(1000) > 0)
                let menuItem = Config.Item("Ult" + ally.ChampionName).GetValue<bool>()
                where
                    menuItem && Config.Item("UltMinHP").GetValue<Slider>().Value >= (ally.Health/ally.MaxHealth)*100 &&
                    R.IsReady()
                select ally)
            {
                R.Cast(ally, Config.Item("UsePackets").GetValue<bool>());
            }
        }

        private static void Heal()
        {
            if (Player.HasBuff("Recall"))
            {
                return;
            }

            foreach (var ally in from ally in ObjectManager.Get<Obj_AI_Hero>()
                .Where(ally => ally.IsAlly && !ally.IsDead)
                let menuItem = Config.Item("Heal" + ally.ChampionName).GetValue<bool>()
                where
                    menuItem && Config.Item("HealMinHP").GetValue<Slider>().Value >= (ally.Health/ally.MaxHealth)*100 &&
                    W.IsReady()
                select ally)
            {
                W.Cast(ally, Config.Item("UsePackets").GetValue<bool>());
            }
        }

        private static float ComboDamage(Obj_AI_Base enemy)
        {
            var damage = 0d;
            if (Q.IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.Q);
            }

            if (Dfg.IsReady())
            {
                damage += Player.GetItemDamage(enemy, Damage.DamageItems.Dfg)/1.2;
            }

            if (E.IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.E);
            }

            if (IgniteSlot != SpellSlot.Unknown && Player.Spellbook.CanUseSpell(IgniteSlot) == SpellState.Ready)
            {
                damage += ObjectManager.Player.GetSummonerSpellDamage(enemy, Damage.SummonerSpell.Ignite);
            }

            return (float) damage*(Dfg.IsReady() ? 1.2f : 1);
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            if (Config.Item("ComboActive").GetValue<KeyBind>().Active)
            {
                Combo();
            }
            else
            {
                if (Config.Item("HarassActive").GetValue<KeyBind>().Active ||
                    Config.Item("HarassActiveT").GetValue<KeyBind>().Active)
                {
                    Harass();
                }

                var laneClear = Config.Item("LaneClearActive").GetValue<KeyBind>().Active;
                if ((laneClear || Config.Item("FreezeActive").GetValue<KeyBind>().Active) &&
                    !Config.Item("SupportMode").GetValue<bool>())
                {
                    Farm(laneClear);
                }

                if (Config.Item("JungleFarmActive").GetValue<KeyBind>().Active)
                {
                    JungleFarm();
                }
            }

            Ultimate();
            Heal();
        }

        private static void Game_OnGameSendPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] != Packet.C2S.Move.Header)
            {
                return;
            }

            var decodedPacket = Packet.C2S.Move.Decoded(args.PacketData);
            if (decodedPacket.MoveType == 3 &&
                (((Obj_AI_Base) Orbwalker.GetTarget()).IsMinion && Config.Item("SupportMode").GetValue<bool>()))
            {
                args.Process = false;
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active)
                {
                    Utility.DrawCircle(Player.Position, spell.Range, menuItem.Color);
                }
            }

            var target = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            var eDamage = 20 + ((E.Level - 1)*10) + (Player.BaseAbilityDamage*0.25);
            if (Config.Item("ComboDamage").GetValue<bool>())
            {
                Drawing.DrawText(target.ServerPosition.X, target.ServerPosition.Y, Color.White,
                    ((target.Health - ComboDamage(target))/
                     (RighteousFuryActive ? (eDamage) : (Player.GetAutoAttackDamage(target)))).ToString(
                         CultureInfo.InvariantCulture));
            }
        }
    }
}