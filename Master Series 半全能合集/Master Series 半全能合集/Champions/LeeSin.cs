﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class LeeSin : Program
    {
        private Obj_AI_Base allyObj = null;
        private bool WardCasted = false, JumpCasted = false, KickCasted = false, FarmCasted = false, InsecJumpCasted = false, QCasted = false, WCasted = false, ECasted = false, RCasted = false;
        private enum HarassStage
        {
            Nothing,
            Doing,
            Finish
        }
        private HarassStage CurHarassStage = HarassStage.Nothing;
        private Vector3 HarassBackPos = default(Vector3), WardPlacePos = default(Vector3);
        private Spell Q2, E2;

        public LeeSin()
        {
            Q = new Spell(SpellSlot.Q, 1000);
            Q2 = new Spell(SpellSlot.Q, 1300);
            W = new Spell(SpellSlot.W, 700);
            E = new Spell(SpellSlot.E, 425);
            E2 = new Spell(SpellSlot.Q, 575);
            R = new Spell(SpellSlot.R, 375);
            Q.SetSkillshot(0.5f, 60, 1800, true, SkillshotType.SkillshotLine);
            Q2.SetTargetted(0.5f, float.MaxValue);
            R.SetTargetted(0.5f, 1500);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWStarCombo", "明星连招", true).SetValue(new KeyBind("X".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWInsecCombo", "明星回旋踢", true).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWKSMob", "抢野怪", true).SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("插件", Name + "Plugin");
            {
                var ComboMenu = new Menu("连招", "Combo");
                {
                    ItemBool(ComboMenu, "Passive", "使用被动", false);
                    ItemBool(ComboMenu, "Q", "使用 Q");
                    ItemBool(ComboMenu, "W", "使用 W");
                    ItemSlider(ComboMenu, "WUnder", "--> 如果血量低于", 30);
                    ItemBool(ComboMenu, "E", "使用 E");
                    ItemBool(ComboMenu, "R", "如果能击杀使用R");
                    ItemBool(ComboMenu, "Item", "使用 物品");
                    ItemBool(ComboMenu, "Ignite", "如果能击杀自动点燃");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("骚扰", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "使用 Q");
                    ItemSlider(HarassMenu, "Q2Above", "-> Q2 如果血量低于", 20);
                    ItemBool(HarassMenu, "E", "使用 E");
                    ItemBool(HarassMenu, "W", "使用W逃跑");
                    ItemBool(HarassMenu, "WWard", "-> 如果附近没有盟友,顺眼逃跑", false);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("清线/清野", "Clear");
                {
                    var SmiteMob = new Menu("如果惩戒能击杀野怪", "SmiteMob");
                    {
                        ItemBool(SmiteMob, "Baron", "大龙");
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
                    ItemBool(ClearMenu, "W", "使用 W");
                    ItemBool(ClearMenu, "E", "使用 E");
                    ItemBool(ClearMenu, "Item", "使用九头蛇");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var InsecMenu = new Menu("明星回旋踢设置", "Insec");
                {
                    var InsecNearMenu = new Menu("盟友在附近配置", "InsecNear");
                    {
                        ItemBool(InsecNearMenu, "ToChamp", "英雄");
                        ItemSlider(InsecNearMenu, "ToChampHp", "-> 如果血量低于", 20);
                        ItemSlider(InsecNearMenu, "ToChampR", "-> 如果在", 1100, 500, 1600);
                        ItemBool(InsecNearMenu, "DrawToChamp", "-> 显示范围", false);
                        ItemBool(InsecNearMenu, "ToTower", "塔");
                        ItemBool(InsecNearMenu, "ToMinion", "小兵");
                        ItemSlider(InsecNearMenu, "ToMinionR", "-> 如果在", 1100, 500, 1600);
                        ItemBool(InsecNearMenu, "DrawToMinion", "-> 显示范围", false);
                        InsecMenu.AddSubMenu(InsecNearMenu);
                    }
                    ItemList(InsecMenu, "Mode", "模式", new[] { "附近有盟友", "选定盟友", "鼠标的位置" });
                    ItemBool(InsecMenu, "Flash", "如果顺眼没有准备好才闪现", false);
                    ItemBool(InsecMenu, "DrawLine", "显示明星回旋踢线路");
                    ChampMenu.AddSubMenu(InsecMenu);
                }
                var UltiMenu = new Menu("大招", "Ultimate");
                {
                    var KillableMenu = new Menu("能击杀", "Killable");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) ItemBool(KillableMenu, Obj.ChampionName, "Use R On " + Obj.ChampionName);
                        UltiMenu.AddSubMenu(KillableMenu);
                    }
                    var InterruptMenu = new Menu("打断", "Interrupt");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in Interrupter.Spells.Where(i => i.ChampionName == Obj.ChampionName)) ItemBool(InterruptMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "Spell " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        UltiMenu.AddSubMenu(InterruptMenu);
                    }
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("额外选项", "Misc");
                {
                    ItemBool(MiscMenu, "WJPink", "使用真眼顺眼", false);
                    ItemBool(MiscMenu, "QLastHit", "使用Q补刀", false);
                    ItemBool(MiscMenu, "RInterrupt", "使用R打断");
                    ItemBool(MiscMenu, "InterruptGap", "-> 如果附近没有盟友,顺眼到后方");
                    ItemBool(MiscMenu, "WSurvive", "尝试使用W求生");
                    ItemBool(MiscMenu, "SmiteCol", "自动惩戒碰撞");
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
            Game.OnWndProc += OnWndProc;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (ItemList("Insec", "Mode") == 1)
            {
                if (R.IsReady())
                {
                    allyObj = allyObj.IsValidTarget(float.MaxValue, false) ? allyObj : null;
                }
                else if (allyObj != null) allyObj = null;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalk.Mode.Combo:
                    NormalCombo();
                    break;
                case Orbwalk.Mode.Harass:
                    Harass();
                    break;
                case Orbwalk.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LastHit:
                    LastHit();
                    break;
                case Orbwalk.Mode.Flee:
                    WardJump(Game.CursorPos);
                    break;
            }
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Harass) CurHarassStage = HarassStage.Nothing;
            if (ItemActive("StarCombo")) StarCombo();
            if (ItemActive("InsecCombo"))
            {
                InsecCombo();
            }
            else InsecJumpCasted = false;
            if (ItemActive("KSMob")) KillStealMob();
            if (ItemBool("Misc", "WSurvive") && W.IsReady() && W.Instance.Name == "BlindMonkWOne") TrySurvive(W.Slot);
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Instance.Name == "BlindMonkQOne" ? Q.Range : Q2.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Instance.Name == "BlindMonkWOne" ? W.Range : 0, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Instance.Name == "BlindMonkEOne" ? E.Range : E2.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Insec", "DrawLine") && R.IsReady())
            {
                byte validTargets = 0;
                if (targetObj != null)
                {
                    Utility.DrawCircle(targetObj.Position, 70, Color.FromArgb(0, 204, 0));
                    validTargets += 1;
                }
                if (GetInsecPos(true) != default(Vector3))
                {
                    Utility.DrawCircle(GetInsecPos(true), 70, Color.FromArgb(0, 204, 0));
                    validTargets += 1;
                }
                if (validTargets == 2) Drawing.DrawLine(Drawing.WorldToScreen(targetObj.Position), Drawing.WorldToScreen(targetObj.Position.Extend(GetInsecPos(true), 600)), 1, Color.White);
            }
            if (ItemList("Insec", "Mode") == 0 && R.IsReady())
            {
                if (ItemBool("InsecNear", "ToChamp") && ItemBool("InsecNear", "DrawToChamp")) Utility.DrawCircle(Player.Position, ItemSlider("InsecNear", "ToChampR"), Color.White);
                if (ItemBool("InsecNear", "ToMinion") && ItemBool("InsecNear", "DrawToMinion")) Utility.DrawCircle(Player.Position, ItemSlider("InsecNear", "ToMinionR"), Color.White);
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "RInterrupt") || !R.IsReady() || !ItemBool("Interrupt", (unit as Obj_AI_Hero).ChampionName + "_" + spell.Slot.ToString()) || Player.IsDead) return;
            if (R.InRange(unit)) R.CastOnUnit(unit, PacketCast());
            if (!R.InRange(unit) && W.CanCast(unit) && W.Instance.Name == "BlindMonkWOne")
            {
                var nearObj = ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(W.Range + i.BoundingRadius, false, Player.Position) && i.IsAlly && !i.IsMe && !(i is Obj_AI_Turret) && i.Distance3D(unit) <= R.Range).OrderBy(i => i.Distance3D(unit));
                if (nearObj.Count() > 0 && !JumpCasted)
                {
                    foreach (var Obj in nearObj)
                    {
                        W.CastOnUnit(Obj, PacketCast());
                        Utility.DelayAction.Add(100, () => R.CastOnUnit(unit, PacketCast()));
                    }
                }
                else if (ItemBool("Misc", "InterruptGap") && (GetWardSlot() != null || WardCasted))
                {
                    WardJump(unit.Position.Randomize(0, (int)R.Range / 2));
                    Utility.DelayAction.Add(100, () => R.CastOnUnit(unit, PacketCast()));
                }
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "BlindMonkQOne")
            {
                QCasted = true;
                Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear ? 2800 : 2200, () => QCasted = false);
            }
            if (args.SData.Name == "BlindMonkWOne")
            {
                WCasted = true;
                Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear ? 2800 : 1000, () => WCasted = false);
                JumpCasted = true;
                Utility.DelayAction.Add(1000, () => JumpCasted = false);
            }
            if (args.SData.Name == "BlindMonkEOne")
            {
                ECasted = true;
                Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear ? 2800 : 2200, () => ECasted = false);
            }
            if (args.SData.Name == "BlindMonkRKick")
            {
                RCasted = true;
                Utility.DelayAction.Add(700, () => RCasted = false);
                if (ItemActive("StarCombo") || ItemActive("InsecCombo"))
                {
                    KickCasted = true;
                    Utility.DelayAction.Add(1000, () => KickCasted = false);
                }
            }
        }

        private void OnWndProc(WndEventArgs args)
        {
            if (args.WParam != 1 || MenuGUI.IsChatOpen || ItemList("Insec", "Mode") != 1 || !R.IsReady()) return;
            allyObj = null;
            if (Player.IsDead) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(80, false, Game.CursorPos) && i.IsAlly && !i.IsMe).OrderBy(i => i.Position.Distance(Game.CursorPos))) allyObj = Obj;
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (ItemBool("Combo", "Passive") && Player.HasBuff("BlindMonkFlurry") && Orbwalk.InAutoAttackRange(targetObj) && Orbwalk.CanAttack()) return;
            if (ItemBool("Combo", "Q") && Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj))
                {
                    var QPred = Q.GetPrediction(targetObj);
                    if (ItemBool("Misc", "SmiteCol") && QPred.CollisionObjects.Count == 1 && Q.MinHitChance == HitChance.High && CastSmite(QPred.CollisionObjects.First()))
                    {
                        Q.Cast(QPred.CastPosition, PacketCast());
                    }
                    else Q.CastIfHitchanceEquals(targetObj, HitChance.High, PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj) && (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 100 || CanKill(targetObj, Q2, 1) || (targetObj.HasBuff("BlindMonkTempest") && E.InRange(targetObj) && !Orbwalk.InAutoAttackRange(targetObj)) || !QCasted)) Q.Cast(PacketCast());
            }
            if (ItemBool("Combo", "E") && E.IsReady())
            {
                if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj))
                {
                    E.Cast(PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj) && (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 30 || !ECasted)) E.Cast(PacketCast());
            }
            if (ItemBool("Combo", "R") && ItemBool("Killable", targetObj.ChampionName) && R.CanCast(targetObj) && (CanKill(targetObj, R) || (CanKill(targetObj, R, R.GetDamage(targetObj), GetQ2Dmg(targetObj, R.GetDamage(targetObj))) && ItemBool("Combo", "Q") && Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave")))) R.CastOnUnit(targetObj, PacketCast());
            if (ItemBool("Combo", "W") && W.IsReady())
            {
                if (W.Instance.Name == "BlindMonkWOne")
                {
                    if (Orbwalk.InAutoAttackRange(targetObj) && Player.HealthPercentage() <= ItemList("Combo", "WUnder")) W.Cast(PacketCast());
                }
                else if (E.InRange(targetObj) && !Player.HasBuff("BlindMonkSafeguard") && !WCasted) W.Cast(PacketCast());
            }
            if (ItemBool("Combo", "Item")) UseItem(targetObj);
            if (ItemBool("Combo", "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null)
            {
                CurHarassStage = HarassStage.Nothing;
                return;
            }
            switch (CurHarassStage)
            {
                case HarassStage.Nothing:
                    CurHarassStage = HarassStage.Doing;
                    break;
                case HarassStage.Doing:
                    if (ItemBool("Harass", "Q") && Q.IsReady())
                    {
                        if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj))
                        {
                            var QPred = Q.GetPrediction(targetObj);
                            if (ItemBool("Misc", "SmiteCol") && QPred.CollisionObjects.Count == 1 && Q.MinHitChance == HitChance.High && CastSmite(QPred.CollisionObjects.First()))
                            {
                                Q.Cast(QPred.CastPosition, PacketCast());
                            }
                            else Q.CastIfHitchanceEquals(targetObj, HitChance.High, PacketCast());
                        }
                        else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj) && (CanKill(targetObj, Q2, 1) || (W.IsReady() && W.Instance.Name == "BlindMonkWOne" && Player.Mana >= W.Instance.ManaCost + (ItemBool("Harass", "E") && E.IsReady() && E.Instance.Name == "BlindMonkEOne" ? Q.Instance.ManaCost + E.Instance.ManaCost : Q.Instance.ManaCost) && Player.HealthPercentage() >= ItemSlider("Harass", "Q2Above"))))
                        {
                            HarassBackPos = Player.ServerPosition;
                            Q.Cast(PacketCast());
                            Utility.DelayAction.Add((int)((Player.Distance3D(targetObj) + (ItemBool("Harass", "E") && E.IsReady() && E.Instance.Name == "BlindMonkEOne" ? E.Range : 0)) / Q.Speed * 1000 - 100) * 2, () => CurHarassStage = HarassStage.Finish);
                        }
                    }
                    if (ItemBool("Harass", "E") && E.IsReady())
                    {
                        if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj))
                        {
                            E.Cast(PacketCast());
                        }
                        else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj)) CurHarassStage = HarassStage.Finish;
                    }
                    break;
                case HarassStage.Finish:
                    if (ItemBool("Harass", "W") && W.IsReady() && W.Instance.Name == "BlindMonkWOne")
                    {
                        var jumpObj = ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(W.Range + i.BoundingRadius, false, Player.Position) && i.IsAlly && !i.IsMe && !(i is Obj_AI_Turret) && i.Distance3D(targetObj) >= 450).OrderByDescending(i => i.Distance3D(Player)).OrderBy(i => ObjectManager.Get<Obj_AI_Turret>().Where(a => a.IsValidTarget(float.MaxValue, false) && a.IsAlly).OrderBy(a => a.Distance3D(Player)).FirstOrDefault().Distance3D(i));
                        if (jumpObj.Count() > 0 && !JumpCasted)
                        {
                            foreach (var Obj in jumpObj) W.CastOnUnit(Obj, PacketCast());
                        }
                        else if (ItemBool("Harass", "WWard") && (GetWardSlot() != null || WardCasted)) WardJump(HarassBackPos);
                    }
                    else
                    {
                        if (HarassBackPos != default(Vector3)) HarassBackPos = default(Vector3);
                        CurHarassStage = HarassStage.Nothing;
                    }
                    break;
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q2.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                var Passive = Player.HasBuff("BlindMonkFlurry");
                if (ItemBool("Clear", "Q") && Q.IsReady())
                {
                    if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(Obj))
                    {
                        Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
                    }
                    else if (Obj.HasBuff("BlindMonkSonicWave") && (CanKill(Obj, Q2, GetQ2Dmg(Obj)) || Player.Distance3D(Obj) > Orbwalk.GetAutoAttackRange(Player, Obj) + 100 || !QCasted || !Passive)) Q.Cast(PacketCast());
                }
                if (ItemBool("Clear", "E") && E.IsReady())
                {
                    if (E.Instance.Name == "BlindMonkEOne" && !Passive && (minionObj.Count(i => E.InRange(i)) >= 2 || (Obj.MaxHealth >= 1200 && E.InRange(Obj))) && !FarmCasted)
                    {
                        E.Cast(PacketCast());
                        FarmCasted = true;
                        Utility.DelayAction.Add(300, () => FarmCasted = false);
                    }
                    else if (Obj.HasBuff("BlindMonkTempest") && E2.InRange(Obj) && (!ECasted || !Passive)) E.Cast(PacketCast());
                }
                if (ItemBool("Clear", "W") && W.IsReady())
                {
                    if (W.Instance.Name == "BlindMonkWOne")
                    {
                        if (!Passive && Orbwalk.InAutoAttackRange(Obj) && !FarmCasted)
                        {
                            W.Cast(PacketCast());
                            FarmCasted = true;
                            Utility.DelayAction.Add(300, () => FarmCasted = false);
                        }
                    }
                    else if (E.InRange(Obj) && (!WCasted || !Passive)) W.Cast(PacketCast());
                }
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady() || Q.Instance.Name != "BlindMonkQOne") return;
            foreach (var Obj in MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly).Where(i => CanKill(i, Q)).OrderByDescending(i => i.Distance3D(Player))) Q.CastIfHitchanceEquals(Obj, HitChance.High, PacketCast());
        }

        private void WardJump(Vector3 Pos)
        {
            if (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || JumpCasted) return;
            bool Casted = false;
            var JumpPos = Pos;
            if (GetWardSlot() != null && !WardCasted && Player.Position.Distance(JumpPos) > GetWardRange()) JumpPos = Player.Position.Extend(JumpPos, GetWardRange());
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(W.Range + i.BoundingRadius, false, Player.Position) && i.IsAlly && !i.IsMe && !(i is Obj_AI_Turret) && i.Position.Distance(WardCasted ? WardPlacePos : JumpPos) < (ItemActive("InsecCombo") ? 400 : 200) && (!ItemActive("InsecCombo") || (ItemActive("InsecCombo") && i.Name.EndsWith("Ward") && i is Obj_AI_Minion))).OrderBy(i => i.Position.Distance(WardCasted ? WardPlacePos : JumpPos)))
            {
                W.CastOnUnit(Obj, PacketCast());
                Casted = true;
                return;
            }
            if (!Casted && GetWardSlot() != null && !WardCasted)
            {
                Player.Spellbook.CastSpell(GetWardSlot().SpellSlot, JumpPos);
                WardPlacePos = JumpPos;
                Utility.DelayAction.Add(800, () => WardPlacePos = default(Vector3));
                WardCasted = true;
                Utility.DelayAction.Add(800, () => WardCasted = false);
            }
        }

        private void StarCombo()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null) return;
            UseItem(targetObj);
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj))
                {
                    var QPred = Q.GetPrediction(targetObj);
                    if (ItemBool("Misc", "SmiteCol") && QPred.CollisionObjects.Count == 1 && Q.MinHitChance == HitChance.High && CastSmite(QPred.CollisionObjects.First()))
                    {
                        Q.Cast(QPred.CastPosition, PacketCast());
                    }
                    else Q.CastIfHitchanceEquals(targetObj, HitChance.High, PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj) && (CanKill(targetObj, Q2, 1) || (!R.IsReady() && !RCasted && KickCasted) || (!R.IsReady() && !RCasted && !KickCasted && (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 100 || !QCasted)))) Q.Cast(PacketCast());
            }
            if (W.IsReady())
            {
                if (W.Instance.Name == "BlindMonkWOne")
                {
                    if (R.IsReady())
                    {
                        if (Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave") && !R.InRange(targetObj) && Player.Distance3D(targetObj) < W.Range + R.Range - 200) WardJump(targetObj.Position.Randomize(0, (int)R.Range / 2));
                    }
                    else if (Orbwalk.InAutoAttackRange(targetObj)) W.Cast(PacketCast());
                }
                else if (E.InRange(targetObj) && !Player.HasBuff("BlindMonkSafeguard") && !WCasted) W.Cast(PacketCast());
            }
            if (R.CanCast(targetObj) && Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave")) R.CastOnUnit(targetObj, PacketCast());
            if (E.IsReady() && !R.IsReady())
            {
                if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj))
                {
                    E.Cast(PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj) && (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 30 || !ECasted)) E.Cast(PacketCast());
            }
        }

        private void InsecCombo()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null) return;
            if (GetInsecPos() != default(Vector3))
            {
                if (R.CanCast(targetObj) && (GetInsecPos(true).Distance(targetObj.Position) - GetInsecPos(true).Distance(Player.Position.Extend(targetObj.Position, targetObj.Distance3D(Player) + 300))) / 300 > 0.7) R.CastOnUnit(targetObj, PacketCast());
                if (W.IsReady() && W.Instance.Name == "BlindMonkWOne" && (GetWardSlot() != null || WardCasted) && Player.Position.Distance(GetInsecPos()) < GetWardRange())
                {
                    WardJump(GetInsecPos());
                    if (ItemBool("Insec", "Flash")) InsecJumpCasted = true;
                    return;
                }
                else if (ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && !WardCasted && Player.Position.Distance(GetInsecPos()) < 400)
                {
                    CastFlash(GetInsecPos());
                    return;
                }
            }
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne")
                {
                    if (Q.InRange(targetObj) && Q.GetPrediction(targetObj).Hitchance >= HitChance.High)
                    {
                        var QPred = Q.GetPrediction(targetObj);
                        if (ItemBool("Misc", "SmiteCol") && QPred.CollisionObjects.Count == 1 && Q.MinHitChance == HitChance.High && CastSmite(QPred.CollisionObjects.First()))
                        {
                            Q.Cast(QPred.CastPosition, PacketCast());
                        }
                        else Q.CastIfHitchanceEquals(targetObj, HitChance.High, PacketCast());
                    }
                    else if (GetInsecPos() != default(Vector3) && Q.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                    {
                        foreach (var Obj in Q.GetPrediction(targetObj, true).CollisionObjects.Where(i => i.Position.Distance(GetInsecPos()) < ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()) && !CanKill(i, Q)).OrderBy(i => i.Position.Distance(GetInsecPos()))) Q.CastIfHitchanceEquals(Obj, HitChance.High, PacketCast());
                    }
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj) && (CanKill(targetObj, Q2, 1) || (!R.IsReady() && !RCasted && KickCasted) || (!R.IsReady() && !RCasted && !KickCasted && (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 100 || !QCasted)) || (GetInsecPos() != default(Vector3) && Player.Position.Distance(GetInsecPos()) > ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()))))
                {
                    Q.Cast(PacketCast());
                }
                else if (GetInsecPos() != default(Vector3) && ObjectManager.Get<Obj_AI_Base>().Any(i => i.HasBuff("BlindMonkSonicWave") && i.IsValidTarget(Q2.Range) && i.Position.Distance(GetInsecPos()) < ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()))) Q.Cast(PacketCast());
            }
        }

        private void KillStealMob()
        {
            var Mob = MinionManager.GetMinions(Q2.Range, MinionTypes.All, MinionTeam.Neutral).FirstOrDefault(i => new string[] { "SRU_Baron", "SRU_Dragon", "SRU_Blue", "SRU_Red" }.Any(a => i.Name.StartsWith(a) && !i.Name.StartsWith(a + "Mini")));
            CustomOrbwalk(Mob);
            if (Mob == null) return;
            if (SmiteReady()) CastSmite(Mob);
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne")
                {
                    if (Q.InRange(Mob) && CanKill(Mob, Q, Q.GetDamage(Mob) + (SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0), GetQ2Dmg(Mob, Q.GetDamage(Mob) + (SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0))) && Q.GetPrediction(Mob).Hitchance >= HitChance.High)
                    {
                        Q.CastIfHitchanceEquals(Mob, HitChance.High, PacketCast());
                    }
                    else if (SmiteReady() && CanKill(Mob, Q2, Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite)) && Q.GetPrediction(Mob).Hitchance <= HitChance.OutOfRange)
                    {
                        foreach (var Obj in Q.GetPrediction(Mob, true).CollisionObjects.Where(i => i.Distance3D(Mob) <= 760 && !CanKill(i, Q)).OrderBy(i => i.Distance3D(Mob))) Q.CastIfHitchanceEquals(Obj, HitChance.High, PacketCast());
                    }
                }
                else if (Mob.HasBuff("BlindMonkSonicWave") && CanKill(Mob, Q2, SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0, GetQ2Dmg(Mob, SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0)))
                {
                    Q.Cast(PacketCast());
                    if (SmiteReady()) Utility.DelayAction.Add((int)((Player.Distance3D(Mob) - 760) / Q.Speed * 1000 + 300), () => CastSmite(Mob, false));
                }
                else if (ObjectManager.Get<Obj_AI_Base>().Any(i => i.HasBuff("BlindMonkSonicWave") && i.IsValidTarget(Q2.Range) && i.Distance3D(Mob) <= 760) && SmiteReady() && CanKill(Mob, Q2, Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite)))
                {
                    Q.Cast(PacketCast());
                    Utility.DelayAction.Add((int)((Player.Distance3D(Mob) - 760) / Q.Speed * 1000 + 300), () => CastSmite(Mob));
                }
            }
        }

        private Vector3 GetInsecPos(bool IsDraw = false)
        {
            if (!R.IsReady()) return default(Vector3);
            switch (ItemList("Insec", "Mode"))
            {
                case 0:
                    var ChampList = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(ItemSlider("InsecNear", "ToChampR"), false) && i.IsAlly && !i.IsMe && i.HealthPercentage() >= ItemSlider("InsecNear", "ToChampHp")).ToList();
                    var TowerObj = ObjectManager.Get<Obj_AI_Turret>().Where(i => i.IsValidTarget(float.MaxValue, false) && i.IsAlly).OrderBy(i => i.Distance3D(Player)).FirstOrDefault();
                    var MinionObj = targetObj != null ? ObjectManager.Get<Obj_AI_Minion>().Where(i => i.IsValidTarget(ItemSlider("InsecNear", "ToMinionR"), false) && i.IsAlly && Player.Distance3D(TowerObj) > 1500 && i.Distance3D(targetObj) > 600 && !i.Name.EndsWith("Ward")).OrderByDescending(i => i.Distance3D(targetObj)).OrderBy(i => i.Distance3D(TowerObj)).FirstOrDefault() : null;
                    if (ChampList.Count > 0 && ItemBool("InsecNear", "ToChamp"))
                    {
                        var Pos = default(Vector3);
                        foreach (var Obj in ChampList) Pos += Obj.Position;
                        Pos = new Vector2(Pos.X / ChampList.Count, Pos.Y / ChampList.Count).To3D();
                        return IsDraw ? Pos : targetObj.Position.Extend(Pos, -230);
                    }
                    if (MinionObj != null && ItemBool("InsecNear", "ToMinion")) return IsDraw ? MinionObj.Position : targetObj.Position.Extend(MinionObj.Position, -230);
                    if (TowerObj != null && ItemBool("InsecNear", "ToTower")) return IsDraw ? TowerObj.Position : targetObj.Position.Extend(TowerObj.Position, -230);
                    break;
                case 1:
                    if (allyObj != null) return IsDraw ? allyObj.Position : targetObj.Position.Extend(allyObj.Position, -230);
                    break;
                case 2:
                    return IsDraw ? Game.CursorPos : targetObj.Position.Extend(Game.CursorPos, -230);
            }
            return default(Vector3);
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }

        private double GetQ2Dmg(Obj_AI_Base Target, double Plus = 0)
        {
            var Dmg = Player.CalcDamage(Target, Damage.DamageType.Physical, new double[] { 50, 80, 110, 140, 170 }[Q.Level - 1] + 0.9 * Player.FlatPhysicalDamageMod + 0.08 * (Target.MaxHealth - Target.Health + Plus));
            return Target is Obj_AI_Minion && Dmg > 400 ? Player.CalcDamage(Target, Damage.DamageType.Physical, 400) : Dmg;
        }
    }
}