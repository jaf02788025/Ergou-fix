﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Lucian : Program
    {
        private bool QCasted = false, WCasted = false, ECasted = false, WillInAA = false;
        private Spell Q2;
        private Vector2 REndPos = default(Vector2);

        public Lucian()
        {
            Q = new Spell(SpellSlot.Q, 640);
            Q2 = new Spell(SpellSlot.Q, 1100);
            W = new Spell(SpellSlot.W, 1000);
            E = new Spell(SpellSlot.E, 445);
            R = new Spell(SpellSlot.R, 1400);
            Q.SetTargetted(0.35f, 500);
            Q2.SetSkillshot(0.35f, 65, 500, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.2f, 80, 500, true, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.2f, 120, 500, true, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("插件", Name + "Plugin");
            {
                var ComboMenu = new Menu("连招", "Combo");
                {
                    ItemBool(ComboMenu, "Passive", "使用被动");
                    ItemBool(ComboMenu, "Q", "使用 Q");
                    ItemBool(ComboMenu, "W", "使用 W");
                    ItemBool(ComboMenu, "E", "使用 E");
                    ItemSlider(ComboMenu, "EDelay", "-> 使用E后平A延迟 (毫秒)", 2000, 0, 4000);
                    ItemBool(ComboMenu, "R", "如果能击杀使用R");
                    ItemBool(ComboMenu, "CancelR", "-> 停止使用R抢人头");
                    ItemBool(ComboMenu, "Item", "使用 物品");
                    ItemBool(ComboMenu, "Ignite", "如果能击杀自动点燃");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("骚扰", "Harass");
                {
                    ItemBool(HarassMenu, "Passive", "使用 被动");
                    ItemBool(HarassMenu, "Q", "使用 Q");
                    ItemBool(HarassMenu, "W", "使用 W");
                    ItemBool(HarassMenu, "E", "使用 E");
                    ItemSlider(HarassMenu, "EAbove", "-> 如果血量超出", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("清线/清野", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "使用 Q");
                    ItemBool(ClearMenu, "W", "使用 W");
                    ItemBool(ClearMenu, "E", "使用 E");
                    ItemSlider(ClearMenu, "EDelay", "-> 使用E后平A延迟 (毫秒)", 2000, 0, 4000);
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("额外选项", "Misc");
                {
                    ItemBool(MiscMenu, "QKillSteal", "使用Q抢人头");
                    ItemSlider(MiscMenu, "CustomSkin", "失效-换肤", 2, 0, 2).ValueChanged += SkinChanger;
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
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
            if (Player.IsChannelingImportantSpell())
            {
                if (ItemBool("Combo", "R"))
                {
                    if (Player.CountEnemysInRange((int)R.Range + 60) == 0) R.Cast(PacketCast());
                    if (targetObj != null) LockROnTarget(targetObj);
                }
                return;
            }
            else REndPos = default(Vector2);
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear) LaneJungClear();
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Combo) WillInAA = false;
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q2.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "LucianQ")
            {
                QCasted = true;
                Utility.DelayAction.Add(250, () => QCasted = false);
            }
            if (args.SData.Name == "LucianW")
            {
                WCasted = true;
                Utility.DelayAction.Add(350, () => WCasted = false);
            }
            if (args.SData.Name == "LucianE")
            {
                ECasted = true;
                Utility.DelayAction.Add(250, () => ECasted = false);
            }
        }

        private void AfterAttack(AttackableUnit Target)
        {
            if (!E.IsReady()) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear && ItemBool("Clear", "E") && !HavePassive() && Target is Obj_AI_Minion) || ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || (Orbwalk.CurrentMode == Orbwalk.Mode.Harass && Player.HealthPercentage() >= ItemSlider("Harass", "EAbove"))) && ItemBool(Orbwalk.CurrentMode.ToString(), "E") && !HavePassive(Orbwalk.CurrentMode.ToString()) && Target is Obj_AI_Hero))
            {
                var Pos = (Player.Position.Distance(Game.CursorPos) <= E.Range && Player.Position.Distance(Game.CursorPos) > 100) ? Game.CursorPos : Player.Position.Extend(Game.CursorPos, E.Range);
                if (Target.Position.Distance(Pos) <= Orbwalk.GetAutoAttackRange(Player, Target))
                {
                    E.Cast(Pos, PacketCast());
                    if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo) WillInAA = true;
                }
                WillInAA = false;
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null || Player.IsDashing()) return;
            if (ItemBool(Mode, "Q") && Q.IsReady() && CanKill(targetObj, Q))
            {
                if (Q.InRange(targetObj))
                {
                    Q.CastOnUnit(targetObj, PacketCast());
                }
                else if (Q2.InRange(targetObj)) foreach (var Obj in Q2.GetPrediction(targetObj).CollisionObjects.Where(i => Q.InRange(i) && Q2.WillHit(i.Position, targetObj.Position))) Q.CastOnUnit(Obj, PacketCast());
            }
            if (ItemBool(Mode, "W") && W.CanCast(targetObj) && CanKill(targetObj, W))
            {
                if (W.GetPrediction(targetObj).Hitchance >= HitChance.Low)
                {
                    W.CastIfHitchanceEquals(targetObj, HitChance.Low, PacketCast());
                }
                else if (W.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                {
                    foreach (var Obj in W.GetPrediction(targetObj, true).CollisionObjects.Where(i => i.Distance3D(targetObj) <= W.Width && W.GetPrediction(i).Hitchance >= HitChance.Low)) W.Cast(Obj.Position, PacketCast());
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.CanCast(targetObj) && CanKill(targetObj, R, GetRDmg(targetObj)))
            {
                if (Player.Distance3D(targetObj) > 500 && Player.Distance3D(targetObj) <= 800 && (!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && (!ItemBool(Mode, "W") || (ItemBool(Mode, "W") && !W.IsReady())) && (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && !E.IsReady())))
                {
                    R.Cast(targetObj, PacketCast());
                    REndPos = (Player.Position - targetObj.Position).To2D().Normalized();
                }
                else if (Player.Distance3D(targetObj) > 800 && Player.Distance3D(targetObj) <= 1075)
                {
                    R.Cast(targetObj, PacketCast());
                    REndPos = (Player.Position - targetObj.Position).To2D().Normalized();
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "E") && E.IsReady() && !Orbwalk.InAutoAttackRange(targetObj) && targetObj.Position.Distance(Player.Position.Extend(Game.CursorPos, E.Range)) + 30 <= Orbwalk.GetAutoAttackRange(Player, targetObj)) E.Cast(Game.CursorPos, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
            if (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && (!E.IsReady() || (Mode == "Combo" && E.IsReady() && !WillInAA))))
            {
                if (Mode == "Combo" && ItemBool(Mode, "E") && E.IsReady(ItemSlider(Mode, "EDelay"))) return;
                if (ItemBool(Mode, "Q") && Q.IsReady())
                {
                    if ((Orbwalk.InAutoAttackRange(targetObj) && !HavePassive(Mode)) || (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 50 && Q.InRange(targetObj)))
                    {
                        Q.CastOnUnit(targetObj, PacketCast());
                    }
                    else if (!Q.InRange(targetObj) && Q2.InRange(targetObj))
                    {
                        foreach (var Obj in Q2.GetPrediction(targetObj).CollisionObjects.Where(i => Q.InRange(i) && Q2.WillHit(i.Position, Q2.GetPrediction(targetObj).CastPosition))) Q.CastOnUnit(Obj, PacketCast());
                    }
                }
                if ((!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && ItemBool(Mode, "W") && W.IsReady() && ((Orbwalk.InAutoAttackRange(targetObj) && !HavePassive(Mode)) || (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 50 && W.InRange(targetObj))))
                {
                    if (W.GetPrediction(targetObj).Hitchance >= HitChance.Low)
                    {
                        W.CastIfHitchanceEquals(targetObj, HitChance.Low, PacketCast());
                    }
                    else if (W.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                    {
                        foreach (var Obj in W.GetPrediction(targetObj, true).CollisionObjects.Where(i => i.Distance3D(targetObj) <= W.Width && W.GetPrediction(i).Hitchance >= HitChance.Low)) W.Cast(Obj.Position, PacketCast());
                    }
                }
            }
        }

        private void LaneJungClear()
        {
            if (Player.IsDashing()) return;
            var minionObj = MinionManager.GetMinions(Q2.Range, MinionTypes.All, MinionTeam.NotAlly);
            foreach (var Obj in minionObj)
            {
                if (!ItemBool("Clear", "E") || (ItemBool("Clear", "E") && !E.IsReady()))
                {
                    if (ItemBool("Clear", "E") && E.IsReady(ItemSlider("Clear", "EDelay"))) return;
                    if (ItemBool("Clear", "W") && W.IsReady() && !HavePassive())
                    {
                        if (W.InRange(Obj) && Obj.Team == GameObjectTeam.Neutral && Obj.MaxHealth >= 1200)
                        {
                            W.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
                        }
                        else
                        {
                            var BestW = 0;
                            var BestWPos = default(Vector3);
                            foreach (var Sub in minionObj.Where(i => W.InRange(i) && W.GetPrediction(i).Hitchance >= HitChance.Low))
                            {
                                var Hit = W.GetPrediction(Sub, true).CollisionObjects.Count(i => i.Distance3D(Sub) <= W.Width);
                                if (Hit > BestW || BestWPos == default(Vector3))
                                {
                                    BestW = Hit;
                                    BestWPos = Sub.Position;
                                }
                            }
                            if (BestWPos != default(Vector3)) W.Cast(BestWPos, PacketCast());
                        }
                    }
                    if ((!ItemBool("Clear", "W") || (ItemBool("Clear", "W") && !W.IsReady())) && ItemBool("Clear", "Q") && Q.IsReady() && !HavePassive())
                    {
                        if (Q.InRange(Obj) && Obj.Team == GameObjectTeam.Neutral && Obj.MaxHealth >= 1200)
                        {
                            Q.CastOnUnit(Obj, PacketCast());
                        }
                        else
                        {
                            var BestQ = 0;
                            Obj_AI_Base BestQTarget = null;
                            foreach (var Sub in minionObj.OrderByDescending(i => i.Distance3D(Player)))
                            {
                                var Hit = Q2.GetPrediction(Sub).CollisionObjects.Count(i => Q2.WillHit(i.Position, Q2.GetPrediction(Sub).CastPosition));
                                if (Hit > BestQ || BestQTarget == null)
                                {
                                    BestQ = Hit;
                                    BestQTarget = Sub;
                                }
                            }
                            if (BestQTarget != null) Q.CastOnUnit(BestQTarget, PacketCast());
                        }
                    }
                }
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady() || Player.IsDashing() || ((!ItemBool("Combo", "R") || (ItemBool("Combo", "R") && !ItemBool("Combo", "CancelR"))) && Player.IsChannelingImportantSpell())) return;
            var CancelR = ItemBool("Combo", "R") && ItemBool("Combo", "CancelR") && Player.IsChannelingImportantSpell();
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(Q2.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                if (Q.InRange(Obj))
                {
                    if (CancelR) R.Cast(PacketCast());
                    Q.CastOnUnit(Obj, PacketCast());
                }
                else
                {
                    foreach (var Col in Q2.GetPrediction(Obj).CollisionObjects.Where(i => Q.InRange(i) && Q2.WillHit(i.Position, Q2.GetPrediction(Obj).CastPosition)))
                    {
                        if (CancelR) R.Cast(PacketCast());
                        Q.CastOnUnit(Col, PacketCast());
                    }
                }
            }
        }

        private void UseItem(Obj_AI_Base Target)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(480) >= 1) Items.UseItem(Youmuu);
        }

        private bool HavePassive(string Mode = "Clear")
        {
            if (Mode != "Clear" && !ItemBool(Mode, "Passive")) return false;
            if (QCasted || WCasted || ECasted || Player.HasBuff("LucianPassiveBuff")) return true;
            return false;
        }

        private double GetRDmg(Obj_AI_Hero Target)
        {
            var Shot = (int)(7.5 + new double[] { 7.5, 9, 10.5 }[R.Level - 1] * 1 / Player.AttackDelay);
            var MaxShot = new int[] { 26, 30, 33 }[R.Level - 1];
            return Player.CalcDamage(Target, Damage.DamageType.Physical, (new double[] { 40, 50, 60 }[R.Level - 1] + 0.25 * Player.FlatPhysicalDamageMod + 0.1 * Player.FlatMagicDamageMod) * (Shot > MaxShot ? MaxShot : Shot));
        }

        private void LockROnTarget(Obj_AI_Hero Target)
        {
            var PredR = R.GetPrediction(Target).CastPosition;
            var Pos = new Vector2(PredR.X + REndPos.X * R.Range * 0.98f, PredR.Y + REndPos.Y * R.Range * 0.98f).To3D();
            var ClosePos = Player.Position.To2D().Closest(new Vector2[] { PredR.To2D(), Pos.To2D() }.ToList()).To3D();
            if (ClosePos.IsValid() && !ClosePos.IsWall() && PredR.Distance(ClosePos) > E.Range)
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, ClosePos);
            }
            else if (Pos.IsValid() && !Pos.IsWall() && PredR.Distance(Pos) < R.Range && PredR.Distance(Pos) > 100)
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, Pos);
            }
            else Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
        }
    }
}