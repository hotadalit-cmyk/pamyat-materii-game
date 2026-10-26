// PM.Domain.Run — показатели героя с расшифровкой источников (v0.8). C# 9, без LINQ.
namespace PM.Domain.Run {
    public struct StatLine {
        public string Key;
        public string Name;
        public string Value;
        public double Raw;
        public string Tooltip;
        public string[] Sources;
    }

    public static class StatSheet {
        public static System.Collections.Generic.List<StatLine> Build(
                double mass, int fragments, int dashCharges,
                MutationSet m, EvolutionState e, SoulMeta meta) {
            var list = new System.Collections.Generic.List<StatLine>();

            // mass
            list.Add(new StatLine {
                Key = "mass",
                Name = "Масса",
                Value = FmtTrim(mass, 2),
                Raw = mass,
                Tooltip = "Текущая биомасса тела. Чем больше — тем крупнее и сильнее герой.",
                Sources = SrcMass(mass, meta)
            });

            // fragments
            int maxFragments = 2 + (int)System.Math.Round(e.Sum(EvoEffectKind.MaxFragments));
            list.Add(new StatLine {
                Key = "fragments",
                Name = "Фрагменты",
                Value = fragments + " / " + maxFragments,
                Raw = maxFragments,
                Tooltip = "Число активных фрагментов после деления.",
                Sources = SrcFragments(e, maxFragments)
            });

            // dash
            int maxDash = (m == null ? 1 : m.DashCharges) + (meta == null ? 0 : meta.BonusDashCharges) + (int)System.Math.Round(e.Sum(EvoEffectKind.MaxDash));
            list.Add(new StatLine {
                Key = "dash",
                Name = "Рывок",
                Value = dashCharges + " / " + maxDash,
                Raw = maxDash,
                Tooltip = "Заряды рывка. Восстанавливаются со временем после использования.",
                Sources = SrcDash(m, meta, e, maxDash)
            });

            // dash_speed
            double dashSpeed = (m == null ? 1.0 : m.DashSpeedMult) * (1.0 + e.Sum(EvoEffectKind.DashSpeedMult));
            list.Add(new StatLine {
                Key = "dash_speed",
                Name = "Скорость рывка",
                Value = "×" + Fmt(dashSpeed, 2),
                Raw = dashSpeed,
                Tooltip = "Множитель скорости рывка — выше значит длиннее и быстрее бросок.",
                Sources = SrcDashSpeed(m, e)
            });

            // damage
            double damage = m == null ? 1.0 : m.DamageDealtMult;
            list.Add(new StatLine {
                Key = "damage",
                Name = "Урон",
                Value = "×" + Fmt(damage, 2),
                Raw = damage,
                Tooltip = "Множитель урона, наносимого при контакте и во время рывка.",
                Sources = SrcDamage(m, e)
            });

            // defense
            double defense = (m == null ? 1.0 : m.DamageTakenMult) * (1.0 + e.Sum(EvoEffectKind.DamageTakenMult));
            list.Add(new StatLine {
                Key = "defense",
                Name = "Защита",
                Value = FmtPct(defense),
                Raw = defense,
                Tooltip = "Доля получаемого урона. Меньше 0% — хорошо, больше — плохо.",
                Sources = SrcDefense(m, e)
            });

            // pull
            double pull = (m == null ? 1.0 : m.PullRadiusMult) + e.Sum(EvoEffectKind.PullFlat);
            list.Add(new StatLine {
                Key = "pull",
                Name = "Притяжение",
                Value = Fmt(pull, 2),
                Raw = pull,
                Tooltip = "Сила, с которой добыча притягивается к телу героя.",
                Sources = SrcPull(m, e)
            });

            // light
            double light = (m == null ? 3.5 : m.LightRadius) + e.Sum(EvoEffectKind.LightFlat);
            list.Add(new StatLine {
                Key = "light",
                Name = "Свет",
                Value = Fmt(light, 1),
                Raw = light,
                Tooltip = "Радиус освещения вокруг героя в темноте пещеры.",
                Sources = SrcLight(m, e)
            });

            // regen
            double regen = (m == null ? 0.0 : m.RegenPerSec) + e.Sum(EvoEffectKind.RegenFlat);
            list.Add(new StatLine {
                Key = "regen",
                Name = "Регенерация",
                Value = Fmt(regen, 2) + "/с",
                Raw = regen,
                Tooltip = "Восстановление массы в секунду, даже без поглощения.",
                Sources = SrcRegen(m, e)
            });

            // absorb
            double absorb = m == null ? 1.25 : m.AbsorbRatio;
            list.Add(new StatLine {
                Key = "absorb",
                Name = "Поглощение",
                Value = "×" + Fmt(absorb, 2),
                Raw = absorb,
                Tooltip = "Доля массы жертвы, переходящая к вам при поглощении.",
                Sources = SrcAbsorb(m)
            });

            // dash_cost
            double dashCost = (m == null ? 1.0 : m.DashCostMult) * (meta == null ? 1.0 : meta.DashCostMult);
            list.Add(new StatLine {
                Key = "dash_cost",
                Name = "Стоимость рывка",
                Value = "×" + Fmt(dashCost, 2),
                Raw = dashCost,
                Tooltip = "Доля массы, расходуемой на рывок. Меньше — дешевле.",
                Sources = SrcDashCost(m, meta)
            });

            // yield
            double yield = 1.0 + e.Sum(EvoEffectKind.YieldMult);
            list.Add(new StatLine {
                Key = "yield",
                Name = "Добыча",
                Value = "×" + Fmt(yield, 2),
                Raw = yield,
                Tooltip = "Множитель выпадаемых ресурсов с поглощённых существ.",
                Sources = SrcYield(e)
            });

            // storage
            double storage = ResourceStore.BaseCapacity + e.Sum(EvoEffectKind.StorageCap);
            list.Add(new StatLine {
                Key = "storage",
                Name = "Хранилище",
                Value = Fmt(storage, 0),
                Raw = storage,
                Tooltip = "Вместимость хранилища для каждого вида ресурсов.",
                Sources = SrcStorage(e)
            });

            // mass_gain
            double massGain = 1.0 + e.Sum(EvoEffectKind.MassGainMult);
            list.Add(new StatLine {
                Key = "mass_gain",
                Name = "Прирост массы",
                Value = "×" + Fmt(massGain, 2),
                Raw = massGain,
                Tooltip = "Множитель массы, получаемой при поглощении существ.",
                Sources = SrcMassGain(e)
            });

            return list;
        }

        // ---------- Sources ----------
        private static string[] SrcMass(double mass, SoulMeta meta) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("Текущая " + Fmt(mass, 2));
            if (meta != null && meta.StartMassBonus > 0) {
                l.Add("Душа: Живучесть ур." + meta.Level(SoulStat.Vitality) + " +" + Fmt(meta.StartMassBonus, 1) + " на старте");
            }
            return l.ToArray();
        }

        private static string[] SrcFragments(EvolutionState e, int maxFragments) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База 2");
            AddEvoSources(l, e, EvoEffectKind.MaxFragments, "+");
            return l.ToArray();
        }

        private static string[] SrcDash(MutationSet m, SoulMeta meta, EvolutionState e, int maxDash) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База 1");
            if (m != null) {
                int dash2 = m.Stacks("m_dash2");
                if (dash2 > 0) l.Add("Второй рывок +" + dash2);
            }
            if (meta != null && meta.BonusDashCharges > 0) {
                l.Add("Душа: Энергия ур." + meta.Level(SoulStat.Energy) + " +" + meta.BonusDashCharges);
            }
            AddEvoSources(l, e, EvoEffectKind.MaxDash, "+");
            return l.ToArray();
        }

        private static string[] SrcDashSpeed(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            AddEvoSources(l, e, EvoEffectKind.DashSpeedMult, "+");
            return l.ToArray();
        }

        private static string[] SrcDamage(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            if (m != null) {
                int spikes = m.Stacks("m_spikes");
                if (spikes > 0) l.Add("Шипы ×" + Fmt(System.Math.Pow(1.5, spikes), 2));
            }
            AddEvoSources(l, e, EvoEffectKind.DamageDealtMult, "+");
            return l.ToArray();
        }

        private static string[] SrcDefense(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            if (m != null) {
                int shell = m.Stacks("m_shell");
                if (shell > 0) l.Add("Панцирь ×" + Fmt(System.Math.Pow(0.7, shell), 2));
            }
            AddEvoSources(l, e, EvoEffectKind.DamageTakenMult, "");
            return l.ToArray();
        }

        private static string[] SrcPull(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            if (m != null) {
                int magnet = m.Stacks("m_magnet");
                if (magnet > 0) l.Add("Притяжение ×" + Fmt(System.Math.Pow(1.5, magnet), 2));
            }
            AddEvoSources(l, e, EvoEffectKind.PullFlat, "+");
            return l.ToArray();
        }

        private static string[] SrcLight(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База 3.5");
            if (m != null) {
                int eyes = m.Stacks("m_eyes");
                if (eyes > 0) l.Add("Глаза +" + (2 * eyes));
                int light = m.Stacks("m_light");
                if (light > 0) l.Add("Биолюминесценция +" + Fmt(1.5 * light, 1));
            }
            AddEvoSources(l, e, EvoEffectKind.LightFlat, "+");
            return l.ToArray();
        }

        private static string[] SrcRegen(MutationSet m, EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База 0");
            if (m != null) {
                int regrow = m.Stacks("m_regrow");
                if (regrow > 0) l.Add("Регенерация +" + Fmt(0.02 * regrow, 2));
            }
            AddEvoSources(l, e, EvoEffectKind.RegenFlat, "+");
            return l.ToArray();
        }

        private static string[] SrcAbsorb(MutationSet m) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.25");
            if (m != null && m.Stacks("m_hunger") > 0) l.Add("Голод ×1.10");
            return l.ToArray();
        }

        private static string[] SrcDashCost(MutationSet m, SoulMeta meta) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            if (m != null) {
                int light = m.Stacks("m_light");
                if (light > 0) l.Add("Биолюминесценция ×" + Fmt(System.Math.Pow(0.6, light), 2));
            }
            if (meta != null && meta.DashCostMult < 1.0) {
                l.Add("Душа: Выносливость ур." + meta.Level(SoulStat.Endurance) + " ×" + Fmt(meta.DashCostMult, 2));
            }
            return l.ToArray();
        }

        private static string[] SrcYield(EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            AddEvoSources(l, e, EvoEffectKind.YieldMult, "+");
            return l.ToArray();
        }

        private static string[] SrcStorage(EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База " + Fmt(ResourceStore.BaseCapacity, 0));
            AddEvoSources(l, e, EvoEffectKind.StorageCap, "+");
            return l.ToArray();
        }

        private static string[] SrcMassGain(EvolutionState e) {
            var l = new System.Collections.Generic.List<string>();
            l.Add("База ×1.00");
            AddEvoSources(l, e, EvoEffectKind.MassGainMult, "+");
            return l.ToArray();
        }

        private static void AddEvoSources(System.Collections.Generic.List<string> src, EvolutionState e, EvoEffectKind k, string prefix) {
            if (e == null) return;
            if (e.Training != null) {
                foreach (var d in TrainCatalog.All()) {
                    int lv = e.Training.Level(d.Key);
                    if (d.Kind == k && lv > 0) src.Add("Прокачка «" + d.Name + "» ур." + lv + " " + prefix + Fmt(d.PerLevel * lv, 3));
                }
            }
            foreach (var id in e.Unlocked) {
                var node = EvolutionTree.Get(id);
                if (node == null || node.Effects == null) continue;
                for (int i = 0; i < node.Effects.Length; i++) {
                    if (node.Effects[i].Kind == k) {
                        string v = Fmt(node.Effects[i].Value, 2);
                        src.Add(node.Name + " " + prefix + v);
                    }
                }
            }
        }

        // ---------- Форматирование ----------
        private static string Fmt(double v, int decimals) {
            if (decimals <= 0) {
                return ((int)System.Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            string fmt = "0." + new string('0', decimals);
            return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FmtTrim(double v, int maxDecimals) {
            if (maxDecimals <= 0) {
                return ((int)System.Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            string fmt = "0." + new string('#', maxDecimals);
            return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FmtPct(double v) {
            // 1.0 = "+0%", 0.7 = "-30%", 1.5 = "+50%"
            double pct = (v - 1.0) * 100.0;
            int rounded = (int)System.Math.Round(pct);
            if (rounded == 0) return "0%";
            if (rounded > 0) return "+" + rounded + "%";
            return "−" + (-rounded) + "%";
        }
    }
}
