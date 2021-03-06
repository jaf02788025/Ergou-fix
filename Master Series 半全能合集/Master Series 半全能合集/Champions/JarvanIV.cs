﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class JarvanIV : Program
    {
        private bool RCasted = false;
        private Vector3 FlagPos = default(Vector3);

        public JarvanIV()
        {
            Q = new Spell(SpellSlot.Q, 820);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E, 860);
            R = new Spell(SpellSlot.R, 650);
            Q.SetSkillshot(0.5f, 70, float.MaxValue, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.5f, 175, 1450, false, SkillshotType.SkillshotCircle);
            R.SetTargetted(0.5f, float.MaxValue);

            //Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWEQFlash", "EQ Flash", true).SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("插件", Name + "Plugin");
            {
                var ComboMenu = new Menu("连招", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "使用 Q");
                    ItemBool(ComboMenu, "W", "使用 W");
                    ItemSlider(ComboMenu, "WUnder", "如果血量低于使用W", 20);
                    ItemBool(ComboMenu, "E", "使用 E");
                    ItemBool(ComboMenu, "R", "使用 R");
                    ItemList(ComboMenu, "RMode", "-> 模式", new[] { "能击杀", "# 敌人" });
                    ItemSlider(ComboMenu, "RAbove", "--> 如果敌人超过", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "使用 物品");
                    ItemBool(ComboMenu, "Ignite", "如果能击杀自动点燃");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("骚扰", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "使用 Q");
                    ItemSlider(HarassMenu, "QAbove", "-> 如果旗帜在血量之上", 20);
                    ItemBool(HarassMenu, "E", "使用 E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("清线/清野", "Clear");
                {
                    var SmiteMob = new Menu("如果能惩戒击杀野怪", "SmiteMob");
                    { ItemBool(SmiteMob, "Baron", "大龙");
                        ItemBool(SmiteMob, "Dragon", "小龙");
                        ItemBool(SmiteMob, "Red", "红BUFF");
                        ItemBool(SmiteMob, "Blue", "蓝BUFF");
                        ItemBool(SmiteMob, "Krug", "石头怪");
                        ItemBool(SmiteMob, "Gromp", "大蛤蟆");
                        ItemBool(SmiteMob, "Raptor", "啄木鸟4兄弟");
                        ItemBool(SmiteMob, "Wolf", "幽灵狼3兄弟");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    ItemBool(ClearMenu, "Q", "使用 Q");
                    ItemBool(ClearMenu, "E", "使用 E");
                    ItemBool(ClearMenu, "Item", "使用九头蛇物品");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("额外选项", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "使用Q补刀");
                    ItemBool(MiscMenu, "QKillSteal", "使用Q抢人头");
                    ItemBool(MiscMenu, "EQInterrupt", "使用EQ打断");
                    ItemBool(MiscMenu, "WSurvive", "尝试使用W求生");
                    ItemSlider(MiscMenu, "CustomSkin", "失效-换肤", 5, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("显示范围", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q 范围", false);
                    ItemBool(DrawMenu, "W", "W 范围", false);
                    ItemBool(DrawMenu, "E", "E 范围", false);
                    ItemBool(DrawMenu, "R", "R 范围", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnProcessSpellCast += TrySurviveSpellCast;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            //if (ItemActive("EQFlash")) EQFlash();
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
            if (ItemBool("Misc", "WSurvive") && W.IsReady()) TrySurvive(W.Slot);
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Width, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EQInterrupt") || !Q.IsReady()) return;
            if (Q.InRange(unit) && E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(unit.Position.Extend(Player.Position, -100), PacketCast());
            if (FlagPos != default(Vector3) && (FlagPos.Distance(unit.Position) <= 60 || (Q.WillHit(unit.Position, FlagPos, 110) && Player.Distance3D(unit) > 50))) Q.Cast(FlagPos, PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "JarvanIVCataclysm" && ItemBool("Combo", "R"))
            {
                RCasted = true;
                Utility.DelayAction.Add(3500, () => RCasted = false);
            }
            if (args.SData.Name == "JarvanIVDemacianStandard")
            {
                FlagPos = args.End;
                Utility.DelayAction.Add(8050, () => FlagPos = default(Vector3));
            }
        }

        private void NormalCombo(string Mode)
        {
            if (Mode == "Combo" && ItemBool(Mode, "R") && ItemList(Mode, "RMode") == 0 && R.IsReady() && RCasted && Player.CountEnemysInRange(325) == 0) R.Cast(PacketCast());
            if (targetObj == null) return;
            if (ItemBool(Mode, "E") && E.CanCast(targetObj)) E.Cast((Player.Distance3D(targetObj) > 450 && !targetObj.IsFacing(Player)) ? targetObj.Position.Extend(Player.Position, Player.Distance3D(targetObj) <= E.Range - 100 ? -100 : 0) : targetObj.Position, PacketCast());
            if ((!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && !E.IsReady())) && ItemBool(Mode, "Q") && Q.IsReady())
            {
                if (ItemBool(Mode, "E") && FlagPos != default(Vector3))
                {
                    if ((FlagPos.Distance(targetObj.Position) <= 60 || (Q.WillHit(targetObj.Position, FlagPos, 110) && Player.Distance3D(targetObj) > 50)) && Q.InRange(FlagPos))
                    {
                        if (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemSlider(Mode, "QAbove"))) Q.Cast(FlagPos, PacketCast());
                    }
                    else if (Q.InRange(targetObj)) Q.Cast(targetObj.Position, PacketCast());
                }
                else if ((!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && FlagPos == default(Vector3))) && Q.InRange(targetObj)) Q.Cast(targetObj.Position, PacketCast());
            }
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.IsReady())
            {
                if (!RCasted)
                {
                    switch (ItemList(Mode, "RMode"))
                    {
                        case 0:
                            if (R.InRange(targetObj) && CanKill(targetObj, R)) R.CastOnUnit(targetObj, PacketCast());
                            break;
                        case 1:
                            var UltiObj = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(R.Range) && (i.CountEnemysInRange(325) >= ItemSlider(Mode, "RAbove") || (CanKill(i, R) && i.CountEnemysInRange(325) >= 1)));
                            if (UltiObj != null) R.CastOnUnit(UltiObj, PacketCast());
                            break;
                    }
                }
                else if (Player.CountEnemysInRange(325) == 0) R.Cast(PacketCast());
            }
            if (Mode == "Combo" && ItemBool(Mode, "W") && W.CanCast(targetObj) && Player.HealthPercentage() <= ItemSlider(Mode, "WUnder")) W.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && (minionObj.Count >= 2 || Obj.MaxHealth >= 1200))
                {
                    var posEFarm1 = E.GetCircularFarmLocation(minionObj.Where(i => !i.IsMelee()).ToList());
                    var posEFarm2 = E.GetCircularFarmLocation(minionObj);
                    if (posEFarm1.MinionsHit >= 3)
                    {
                        E.Cast(posEFarm1.Position, PacketCast());
                    }
                    else E.Cast(posEFarm2.MinionsHit >= 2 ? posEFarm2.Position : Obj.Position.To2D(), PacketCast());
                }
                if (ItemBool("Clear", "Q") && Q.IsReady())
                {
                    var posQFarm1 = Q.GetLineFarmLocation(minionObj.Where(i => Q.InRange(i) && !i.IsMelee()).ToList());
                    var posQFarm2 = Q.GetLineFarmLocation(minionObj.Where(i => Q.InRange(i)).ToList());
                    if (Q.InRange(Obj) && CanKill(Obj, Q))
                    {
                        Q.Cast(Obj.Position, PacketCast());
                    }
                    else if (posQFarm1.MinionsHit >= 3)
                    {
                        Q.Cast(posQFarm1.Position, PacketCast());
                    }
                    else if (posQFarm2.MinionsHit >= 2)
                    {
                        Q.Cast(posQFarm2.Position, PacketCast());
                    }
                    else if (Q.InRange(Obj)) Q.Cast(Obj, PacketCast());
                }
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly).Where(i => CanKill(i, Q)).OrderByDescending(i => i.Distance3D(Player))) Q.Cast(Obj.Position, PacketCast());
        }

        private void Flee()
        {
            if (!Q.IsReady()) return;
            if (E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(Game.CursorPos, PacketCast());
            if (Player.LastCastedSpellName() == "JarvanIVDemacianStandard") Q.Cast(Game.CursorPos, PacketCast());
        }

        private void EQFlash()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null || !Q.IsReady()) return;
            if (E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(Player.Position.Extend(targetObj.Position, (!Q.InRange(targetObj) && Player.Distance3D(targetObj) <= Q.Range + 370 && FlashReady()) ? Q.Range : targetObj.Distance3D(Player) + (Player.Distance3D(targetObj) <= E.Range - 100 ? 100 : 0)), PacketCast());
            if (FlagPos != default(Vector3) && Q.InRange(FlagPos) && (FlagPos.Distance(targetObj.Position) <= 60 || Q.WillHit(targetObj.Position, FlagPos, 110) || (FlashReady() && Player.Distance3D(targetObj) <= Q.Range + 370)))
            {
                Q.Cast(FlagPos, PacketCast());
                if (FlashReady() && Player.LastCastedSpellName() == "JarvanIVDragonStrike" && (FlagPos.Distance(targetObj.Position) > 60 || !Q.WillHit(targetObj.Position, FlagPos, 110)) && Player.Distance3D(targetObj) <= Q.Range + 370) Utility.DelayAction.Add((int)((Player.Distance3D(targetObj) - Q.Range) / E.Speed * 1000 + 500), () => CastFlash(targetObj.Position));
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(Q.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) Q.Cast(Obj.Position, PacketCast());
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }
    }
}