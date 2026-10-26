// PMHarness — тесты системы эволюции v0.8. C# 9, без LINQ.
using PM.Domain.Run;

namespace PMHarness {
    // ============================================================
    //  Ресурсы и хранилище
    // ============================================================
    public static class EvoResourceTests {
        public static void ResourceInfo_Count_Is6() {
            Check.Eq(ResourceInfo.Count, 6, "ResourceInfo.Count == 6");
        }

        public static void ResourceInfo_Names_AllNonEmpty() {
            for (int i = 0; i < ResourceInfo.Count; i++) {
                var k = (ResourceKind)i;
                Check.True(ResourceInfo.Name(k).Length > 0, "Name " + k + " non-empty");
            }
        }

        public static void ResourceInfo_Descriptions_AllNonEmpty() {
            for (int i = 0; i < ResourceInfo.Count; i++) {
                var k = (ResourceKind)i;
                Check.True(ResourceInfo.Description(k).Length > 0, "Description " + k + " non-empty");
            }
        }

        public static void ResourceInfo_Colors_AllNonZero() {
            for (int i = 0; i < ResourceInfo.Count; i++) {
                var k = (ResourceKind)i;
                Check.True(ResourceInfo.Color(k) != 0, "Color " + k + " non-zero");
            }
        }

        public static void ResourceStore_DefaultCapacity() {
            var s = new ResourceStore();
            Check.Eq(s.Capacity, ResourceStore.BaseCapacity, "default Capacity == BaseCapacity");
            Check.Eq(ResourceStore.BaseCapacity, 40.0, "BaseCapacity == 40");
        }

        public static void ResourceStore_Add_WithinCapacity() {
            var s = new ResourceStore();
            double added = s.Add(ResourceKind.Organic, 15.0);
            Check.Eq(added, 15.0, "added == 15");
            Check.Eq(s.Get(ResourceKind.Organic), 15.0, "Get == 15");
        }

        public static void ResourceStore_Add_OverCapacity_Caps() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 35.0);
            double added = s.Add(ResourceKind.Organic, 10.0);
            Check.Eq(added, 5.0, "second Add returns 5");
            Check.Eq(s.Get(ResourceKind.Organic), 40.0, "Get capped at 40");
        }

        public static void ResourceStore_Add_Full_ReturnsZero() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Chitin, 40.0);
            double added = s.Add(ResourceKind.Chitin, 5.0);
            Check.Eq(added, 0.0, "Add when full returns 0");
        }

        public static void ResourceStore_Add_Negative_ReturnsZero() {
            var s = new ResourceStore();
            double added = s.Add(ResourceKind.Slime, -5.0);
            Check.Eq(added, 0.0, "negative Add returns 0");
            Check.Eq(s.Get(ResourceKind.Slime), 0.0, "Get stays 0");
        }

        public static void ResourceStore_CustomCapacity() {
            var s = new ResourceStore { Capacity = 20.0 };
            double added = s.Add(ResourceKind.Crystal, 25.0);
            Check.Eq(added, 20.0, "Add caps at custom Capacity");
            Check.Eq(s.Get(ResourceKind.Crystal), 20.0, "Get == 20");
        }

        public static void ResourceStore_CanPay_Enough() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 10);
            s.Add(ResourceKind.Chitin, 5);
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 8), new CostItem(ResourceKind.Chitin, 4));
            Check.True(s.CanPay(c), "CanPay with enough");
        }

        public static void ResourceStore_CanPay_NotEnough() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 3);
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 8));
            Check.True(!s.CanPay(c), "CanPay with too little");
        }

        public static void ResourceStore_Pay_Enough_Deducts() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 20);
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 7));
            bool ok = s.Pay(c);
            Check.True(ok, "Pay returns true");
            Check.Eq(s.Get(ResourceKind.Organic), 13.0, "Get after Pay == 13");
        }

        public static void ResourceStore_Pay_NotEnough_NoChange() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 3);
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 7));
            bool ok = s.Pay(c);
            Check.True(!ok, "Pay returns false when not enough");
            Check.Eq(s.Get(ResourceKind.Organic), 3.0, "Get unchanged after failed Pay");
        }

        public static void ResourceStore_Pay_AllOrNothing() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 10);
            s.Add(ResourceKind.Chitin, 1);
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 5), new CostItem(ResourceKind.Chitin, 3));
            bool ok = s.Pay(c);
            Check.True(!ok, "Pay fails when one resource insufficient");
            Check.Eq(s.Get(ResourceKind.Organic), 10.0, "Organic unchanged after failed Pay");
            Check.Eq(s.Get(ResourceKind.Chitin), 1.0, "Chitin unchanged after failed Pay");
        }

        public static void ResourceStore_ToArray_FromArray_RoundTrip() {
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 12);
            s.Add(ResourceKind.Psyche, 7);
            double[] arr = s.ToArray();
            Check.Eq(arr.Length, ResourceInfo.Count, "ToArray length == 6");
            var s2 = ResourceStore.FromArray(arr, ResourceStore.BaseCapacity);
            Check.Eq(s2.Get(ResourceKind.Organic), 12.0, "FromArray Organic");
            Check.Eq(s2.Get(ResourceKind.Psyche), 7.0, "FromArray Psyche");
            Check.Eq(s2.Get(ResourceKind.Slime), 0.0, "FromArray untouched Slime == 0");
        }

        public static void ResourceStore_FromArray_ClampsToCapacity() {
            double[] arr = { 100, 50, -5, 10, 0, 0 };
            var s = ResourceStore.FromArray(arr, 30.0);
            Check.Eq(s.Get(ResourceKind.Organic), 30.0, "clamped to 30");
            Check.Eq(s.Get(ResourceKind.Slime), 0.0, "negative clamped to 0");
            Check.Eq(s.Capacity, 30.0, "Capacity preserved");
        }

        public static void Cost_ToString_SingleItem() {
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 12));
            Check.Eq(c.ToString(), "12 Органика", "Cost single item");
        }

        public static void Cost_ToString_MultiItem() {
            var c = Cost.Of(new CostItem(ResourceKind.Organic, 12), new CostItem(ResourceKind.Chitin, 4));
            Check.Eq(c.ToString(), "12 Органика · 4 Хитин", "Cost multi item");
        }

        public static void Cost_ToString_Empty() {
            var c = Cost.Of();
            Check.Eq(c.ToString(), "—", "Cost empty");
        }

        public static void Cost_ToString_FractionalNoTrailingZero() {
            var c = Cost.Of(new CostItem(ResourceKind.Slime, 7.5));
            Check.Eq(c.ToString(), "7.5 Слизь", "Cost fractional");
        }

        public static void YieldTable_Spore() {
            var items = YieldTable.For("spore", 2.0);
            Check.Eq(items.Length, 2, "spore: 2 items");
            Check.Eq(items[0].Kind, ResourceKind.Organic, "spore: Organic");
            Check.Eq(items[0].Amount, 1.6, "spore: 0.8*2=1.6");
            Check.Eq(items[1].Kind, ResourceKind.Slime, "spore: Slime (v0.9)");
        }

        public static void YieldTable_Mite() {
            var items = YieldTable.For("mite", 1.0);
            Check.Eq(items.Length, 2, "mite: 2 items");
            Check.Eq(items[0].Amount, 0.8, "mite: Chitin 0.8");
            Check.Eq(items[1].Kind, ResourceKind.Slime, "mite: Slime (v0.9)");
        }

        public static void YieldTable_Beetle() {
            var b = YieldTable.For("beetle", 1.0);
            Check.Eq(b[0].Kind, ResourceKind.Chitin, "beetle: Chitin");
            Check.Eq(b[1].Kind, ResourceKind.Organic, "beetle: Organic");
        }

        public static void YieldTable_Jelly() {
            var items = YieldTable.For("jelly", 2.0);
            Check.Eq(items.Length, 2, "jelly: 2 items");
            Check.Eq(items[0].Kind, ResourceKind.Slime, "jelly: Slime");
            Check.Eq(items[0].Amount, 1.8, "jelly: 0.9*2=1.8");
        }

        public static void YieldTable_Leech_And_BubbleJelly_SameAsJelly() {
            var j = YieldTable.For("jelly", 1.0);
            var l = YieldTable.For("leech", 1.0);
            var b = YieldTable.For("bubble_jelly", 1.0);
            Check.Eq(j.Length, l.Length, "jelly/leech same length");
            Check.Eq(j.Length, b.Length, "jelly/bubble_jelly same length");
        }

        public static void YieldTable_CrystalMite() {
            var items = YieldTable.For("crystal_mite", 2.0);
            Check.Eq(items.Length, 1, "crystal_mite: 1 item");
            Check.Eq(items[0].Kind, ResourceKind.Crystal, "crystal_mite: Crystal");
            Check.Eq(items[0].Amount, 1.6, "crystal_mite: 0.8*2=1.6");
        }

        public static void YieldTable_ShardCrab_SameAsCrystalMite() {
            var a = YieldTable.For("crystal_mite", 1.5);
            var b = YieldTable.For("shard_crab", 1.5);
            Check.Eq(a[0].Kind, b[0].Kind, "same Crystal");
            Check.Eq(a[0].Amount, b[0].Amount, "same amount");
        }

        public static void YieldTable_EmberBeetle() {
            var items = YieldTable.For("ember_beetle", 2.0);
            Check.Eq(items.Length, 2, "ember_beetle: 2 items");
            Check.Eq(items[0].Kind, ResourceKind.Ember, "ember_beetle: Ember");
            Check.Eq(items[0].Amount, 1.6, "ember_beetle: 0.8*2=1.6");
            Check.Eq(items[1].Kind, ResourceKind.Chitin, "ember_beetle: Chitin");
        }

        public static void YieldTable_Salamander_SameAsEmberBeetle() {
            var a = YieldTable.For("ember_beetle", 1.0);
            var b = YieldTable.For("salamander", 1.0);
            Check.Eq(a.Length, b.Length, "same length");
            Check.Eq(a[0].Amount, b[0].Amount, "same Ember amount");
        }

        public static void YieldTable_Glowworm() {
            var items = YieldTable.For("glowworm", 2.0);
            Check.Eq(items.Length, 2, "glowworm: 2 items");
            Check.Eq(items[0].Kind, ResourceKind.Psyche, "glowworm: Psyche");
            Check.Eq(items[0].Amount, 1.0, "glowworm: 0.5*2=1.0");
        }

        public static void YieldTable_Wisp_SameAsGlowworm() {
            var a = YieldTable.For("glowworm", 1.0);
            var b = YieldTable.For("wisp", 1.0);
            Check.Eq(a.Length, b.Length, "same length");
            Check.Eq(a[0].Amount, b[0].Amount, "same Psyche amount");
        }

        public static void YieldTable_Spikeback() {
            var items = YieldTable.For("spikeback", 2.0);
            Check.Eq(items.Length, 1, "spikeback: 1 item");
            Check.Eq(items[0].Kind, ResourceKind.Chitin, "spikeback: Chitin");
            Check.Eq(items[0].Amount, 2.0, "spikeback: 1.0*2=2.0");
        }

        public static void YieldTable_SoulSlime() {
            var items = YieldTable.For("soul_slime", 2.0);
            Check.Eq(items.Length, 2, "soul_slime: Psyche + Slime");
            Check.Eq(items[0].Kind, ResourceKind.Psyche, "soul_slime: Psyche");
            Check.Eq(items[0].Amount, 2.4, "soul_slime: 1.2*2=2.4");
        }

        public static void YieldTable_Warden_AllResources15() {
            var items = YieldTable.For("warden", 30.0);
            Check.Eq(items.Length, 6, "warden: 6 items");
            for (int i = 0; i < 6; i++) {
                Check.Eq(items[i].Amount, 15.0, "warden item " + i + " = 15");
            }
        }

        public static void YieldTable_Warden_IgnoresMass() {
            var a = YieldTable.For("warden", 30.0);
            var b = YieldTable.For("warden", 100.0);
            for (int i = 0; i < 6; i++) {
                Check.Eq(a[i].Amount, b[i].Amount, "warden mass-independent " + i);
            }
        }

        public static void YieldTable_Unknown_FallsBackOrganic() {
            var items = YieldTable.For("does_not_exist", 2.0);
            Check.Eq(items.Length, 1, "unknown: 1 item");
            Check.Eq(items[0].Kind, ResourceKind.Organic, "unknown: Organic");
            Check.Eq(items[0].Amount, 1.0, "unknown: 0.5*2=1.0");
        }

        public static void YieldTable_ProportionalToMass() {
            var a = YieldTable.For("mite", 1.0);
            var b = YieldTable.For("mite", 3.0);
            Check.Eq(b[0].Amount, a[0].Amount * 3.0, "Chitin scales 3x");
            Check.Eq(b[1].Amount, a[1].Amount * 3.0, "second item scales 3x");
        }

        public static void YieldTable_AllCatalogSpecies_NonEmpty() {
            var species = SpeciesCatalog.All();
            for (int i = 0; i < species.Length; i++) {
                var items = YieldTable.For(species[i].Id, 1.0);
                Check.True(items.Length > 0, species[i].Id + " yields non-empty");
            }
        }
    }

    // ============================================================
    //  Дерево эволюции
    // ============================================================
    public static class EvoTreeTests {
        public static void All_AtLeast30Nodes() {
            var all = EvolutionTree.All();
            Check.True(all.Length >= 30, "All length >= 30, got " + all.Length);
        }

        public static void All_All5BranchesPresent() {
            var all = EvolutionTree.All();
            bool[] seen = new bool[EvolutionTree.BranchCount];
            for (int i = 0; i < all.Length; i++) seen[(int)all[i].Branch] = true;
            for (int b = 0; b < EvolutionTree.BranchCount; b++) Check.True(seen[b], "branch " + b + " present");
        }

        public static void All_Tiers1to4_AllPresent() {
            var all = EvolutionTree.All();
            bool[] seen = new bool[5];
            for (int i = 0; i < all.Length; i++) {
                int t = all[i].Tier;
                Check.True(t >= 1 && t <= 4, "tier in 1..4: " + all[i].Id);
                seen[t] = true;
            }
            for (int t = 1; t <= 4; t++) Check.True(seen[t], "tier " + t + " present");
        }

        public static void All_EachNodeHasNameDescCostEffects() {
            var all = EvolutionTree.All();
            for (int i = 0; i < all.Length; i++) {
                var n = all[i];
                Check.True(n.Id != null && n.Id.Length > 0, n.Id + " has Id");
                Check.True(n.Name != null && n.Name.Length > 0, n.Id + " has Name");
                Check.True(n.Description != null && n.Description.Length > 0, n.Id + " has Description");
                Check.True(n.Cost != null, n.Id + " has Cost");
                Check.True(n.Effects != null && n.Effects.Length > 0, n.Id + " has Effects");
            }
        }

        public static void All_DescriptionsUnder90Chars() {
            var all = EvolutionTree.All();
            for (int i = 0; i < all.Length; i++) {
                Check.True(all[i].Description.Length <= 90, all[i].Id + " desc <= 90 chars (got " + all[i].Description.Length + ")");
            }
        }

        public static void All_Requires_PointToLowerTier() {
            var all = EvolutionTree.All();
            for (int i = 0; i < all.Length; i++) {
                var n = all[i];
                for (int r = 0; r < n.Requires.Length; r++) {
                    var parent = EvolutionTree.Get(n.Requires[r]);
                    Check.True(parent != null, n.Id + " parent exists: " + n.Requires[r]);
                    if (parent != null) {
                        Check.True(parent.Tier < n.Tier, n.Id + " parent tier < node tier");
                    }
                }
            }
        }

        public static void All_Requires_NoCycles() {
            // Поскольку все Requires идут в меньший тир, цикл невозможен по построению.
            // Доп. проверка: каждый узел достижим из базовых (тир 1) по обратным ссылкам.
            var all = EvolutionTree.All();
            for (int i = 0; i < all.Length; i++) {
                var n = all[i];
                int safety = 16;
                while (n.Tier > 1 && safety-- > 0) {
                    Check.True(n.Requires.Length > 0, n.Id + " non-base must have Requires");
                    if (n.Requires.Length == 0) break;
                    n = EvolutionTree.Get(n.Requires[0]);
                    Check.True(n != null, "parent exists in chain");
                    if (n == null) break;
                }
                Check.True(safety > 0, "chain terminates");
            }
        }

        public static void All_EachBranchHasFork() {
            for (int b = 0; b < 5; b++) {
                var nodes = EvolutionTree.Branch((EvoBranch)b);
                bool hasFork = false;
                for (int i = 0; i < nodes.Length; i++) {
                    if (nodes[i].ExclusiveGroup != null && (nodes[i].Tier == 2 || nodes[i].Tier == 3)) {
                        // должен быть ещё хотя бы один узел с той же группой
                        for (int j = 0; j < nodes.Length; j++) {
                            if (i != j && nodes[j].ExclusiveGroup == nodes[i].ExclusiveGroup) {
                                hasFork = true; break;
                            }
                        }
                    }
                    if (hasFork) break;
                }
                Check.True(hasFork, "branch " + EvolutionTree.BranchName((EvoBranch)b) + " has fork");
            }
        }

        public static void All_11GeneMutationsPresent() {
            string[] expectedGenes = { "m_light", "m_shell", "m_eyes", "m_spikes", "m_sticky",
                                        "m_crystal", "m_hunger", "m_heatproof", "m_vampire",
                                        "m_ricochet", "m_echo" };
            var all = EvolutionTree.All();
            for (int g = 0; g < expectedGenes.Length; g++) {
                bool found = false;
                for (int i = 0; i < all.Length; i++) {
                    if (all[i].Effects == null) continue;
                    for (int e = 0; e < all[i].Effects.Length; e++) {
                        if (all[i].Effects[e].Kind == EvoEffectKind.Mutation
                            && all[i].Effects[e].MutationId == expectedGenes[g]) {
                            found = true;
                            // Узел-ген должен иметь RequiredGene
                            Check.True(all[i].RequiredGene != null, expectedGenes[g] + " node has RequiredGene");
                            break;
                        }
                    }
                    if (found) break;
                }
                Check.True(found, "gene " + expectedGenes[g] + " has node");
            }
        }

        public static void All_GeneMutations_MatchSpeciesGenes() {
            // Каждый вид с GeneMutationId должен иметь соответствующий узел-ген в дереве.
            var species = SpeciesCatalog.All();
            var all = EvolutionTree.All();
            for (int s = 0; s < species.Length; s++) {
                string gene = species[s].GeneMutationId;
                if (gene == null) continue;
                bool found = false;
                string nodeWithGene = null;
                for (int i = 0; i < all.Length; i++) {
                    if (all[i].Effects == null) continue;
                    for (int e = 0; e < all[i].Effects.Length; e++) {
                        if (all[i].Effects[e].Kind == EvoEffectKind.Mutation
                            && all[i].Effects[e].MutationId == gene) {
                            found = true;
                            nodeWithGene = all[i].Id;
                            break;
                        }
                    }
                    if (found) break;
                }
                Check.True(found, "gene " + gene + " from species " + species[s].Id + " has node");
                if (found) {
                    // Найдём узел и проверим RequiredGene == species.Id
                    // один ген могут давать несколько видов (m_echo: bubble_jelly и wisp) — узел требует любой из них
                    var node = EvolutionTree.Get(nodeWithGene);
                    var req = SpeciesCatalog.Get(node.RequiredGene);
                    Check.True(req != null && req.GeneMutationId == gene, "gene " + gene + " node requires a species with that gene");
                }
            }
        }

        public static void All_CostsGrowWithTier() {
            var all = EvolutionTree.All();
            // средняя суммарная стоимость по тирам монотонно растёт
            double[] sumByTier = new double[5];
            int[] cntByTier = new int[5];
            for (int i = 0; i < all.Length; i++) {
                double c = 0;
                if (all[i].Cost != null && all[i].Cost.Items != null) {
                    for (int k = 0; k < all[i].Cost.Items.Length; k++) c += all[i].Cost.Items[k].Amount;
                }
                sumByTier[all[i].Tier] += c;
                cntByTier[all[i].Tier]++;
            }
            // тир1 <= тир2 <= тир3 <= тир4 (по среднему)
            for (int t = 1; t < 4; t++) {
                double avg1 = cntByTier[t] > 0 ? sumByTier[t] / cntByTier[t] : 0;
                double avg2 = cntByTier[t + 1] > 0 ? sumByTier[t + 1] / cntByTier[t + 1] : 0;
                Check.True(avg2 >= avg1, "avg cost tier " + (t + 1) + " >= tier " + t);
            }
            // тир1 в диапазоне 6..14, тир4 в диапазоне 35..70
            double avgT1 = sumByTier[1] / cntByTier[1];
            double avgT4 = sumByTier[4] / cntByTier[4];
            Check.True(avgT1 >= 6 && avgT1 <= 14, "tier1 avg in 6..14, got " + avgT1);
            Check.True(avgT4 >= 35 && avgT4 <= 70, "tier4 avg in 35..70, got " + avgT4);
        }

        public static void Get_Existing() {
            var n = EvolutionTree.Get("body_thick_skin");
            Check.True(n != null, "Get body_thick_skin");
            Check.Eq(n.Name, "Густая кожа", "name matches");
            Check.Eq(n.Tier, 1, "tier 1");
            Check.Eq(n.Branch, EvoBranch.Body, "Body branch");
        }

        public static void Get_Unknown_Null() {
            var n = EvolutionTree.Get("does_not_exist");
            Check.True(n == null, "Get unknown returns null");
        }

        public static void Branch_ReturnsOnlyThatBranch() {
            var body = EvolutionTree.Branch(EvoBranch.Body);
            for (int i = 0; i < body.Length; i++)
                Check.Eq(body[i].Branch, EvoBranch.Body, "Branch(Body) item " + i);
            Check.True(body.Length >= 6, "Body has >= 6 nodes, got " + body.Length);
        }

        public static void BranchName_All5() {
            Check.Eq(EvolutionTree.BranchName(EvoBranch.Body),     "Тело",     "Body name");
            Check.Eq(EvolutionTree.BranchName(EvoBranch.Motion),   "Движение", "Motion name");
            Check.Eq(EvolutionTree.BranchName(EvoBranch.Hunt),     "Охота",    "Hunt name");
            Check.Eq(EvolutionTree.BranchName(EvoBranch.Senses),   "Чувства",  "Senses name");
            Check.Eq(EvolutionTree.BranchName(EvoBranch.Division), "Деление",  "Division name");
        }

        public static void ExclusiveGroups_HaveAtLeastTwoNodes() {
            var all = EvolutionTree.All();
            var groups = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < all.Length; i++) {
                if (all[i].ExclusiveGroup != null) groups.Add(all[i].ExclusiveGroup);
            }
            foreach (var g in groups) {
                int count = 0;
                for (int i = 0; i < all.Length; i++) {
                    if (all[i].ExclusiveGroup == g) count++;
                }
                Check.True(count >= 2, "exclusive group " + g + " has >= 2 nodes (got " + count + ")");
            }
        }
    }

    // ============================================================
    //  Состояние эволюции
    // ============================================================
    public static class EvoStateTests {
        public static void CanUnlock_FreshRoot_None() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Capacity = 100;
            s.Add(ResourceKind.Organic, 50);
            s.Add(ResourceKind.Chitin, 10);
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlock("body_thick_skin", s, known), UnlockBlock.None, "fresh body_thick_skin");
        }

        public static void CanUnlock_AlreadyUnlocked() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin" });
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 50);
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlock("body_thick_skin", s, known), UnlockBlock.AlreadyUnlocked, "already unlocked");
        }

        public static void CanUnlock_MissingPrereq() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 50);
            var known = new System.Collections.Generic.List<string> { "spore", "beetle" };
            Check.Eq(e.CanUnlock("body_chitin_plating", s, known), UnlockBlock.MissingPrereq, "missing prereq");
        }

        public static void CanUnlock_ExclusiveTaken() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "body_chitin_plating", "body_bulwark" });
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 100);
            s.Add(ResourceKind.Chitin, 100);
            var known = new System.Collections.Generic.List<string> { "spore", "beetle" };
            Check.Eq(e.CanUnlock("body_adrenaline", s, known), UnlockBlock.ExclusiveTaken, "exclusive taken");
        }

        public static void CanUnlock_GeneUnknown() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin" });
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 100);
            s.Add(ResourceKind.Chitin, 100);
            // beetle не поглощён
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlock("body_chitin_plating", s, known), UnlockBlock.GeneUnknown, "gene unknown");
        }

        public static void CanUnlock_NotEnoughResources() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin" });
            var s = new ResourceStore(); // пусто
            var known = new System.Collections.Generic.List<string> { "spore", "beetle" };
            Check.Eq(e.CanUnlock("body_chitin_plating", s, known), UnlockBlock.NotEnoughResources, "not enough resources");
        }

        public static void CanUnlock_UnknownNode_MissingPrereq() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlock("does_not_exist", s, known), UnlockBlock.MissingPrereq, "unknown node");
        }

        public static void TryUnlock_PaysAndAppliesAndMarks() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Capacity = 100;
            s.Add(ResourceKind.Organic, 50);
            s.Add(ResourceKind.Chitin, 50);
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            bool ok = e.TryUnlock("body_thick_skin", s, known, mut);
            Check.True(ok, "TryUnlock body_thick_skin");
            Check.Eq(s.Get(ResourceKind.Organic), 42.0, "Organic reduced by 8");
            Check.Eq(s.Get(ResourceKind.Chitin), 48.0, "Chitin reduced by 2");
            Check.True(e.Unlocked.Contains("body_thick_skin"), "marked as unlocked");
        }

        public static void TryUnlock_Failed_NoPayNoMark() {
            var e = new EvolutionState();
            var s = new ResourceStore(); // пусто
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            bool ok = e.TryUnlock("body_thick_skin", s, known, mut);
            Check.True(!ok, "TryUnlock fails");
            Check.Eq(s.Get(ResourceKind.Organic), 0.0, "no pay on fail");
            Check.True(!e.Unlocked.Contains("body_thick_skin"), "not marked on fail");
        }

        public static void TryUnlock_AppliesMutationToSet() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Add(ResourceKind.Psyche, 50);
            s.Add(ResourceKind.Crystal, 50);
            var known = new System.Collections.Generic.List<string> { "spore", "glowworm" };
            var mut = new MutationSet();
            bool ok = e.TryUnlock("senses_eyes", s, known, mut);
            Check.True(ok, "TryUnlock senses_eyes");
            Check.Eq(mut.Stacks("m_eyes"), 1, "m_eyes added");
        }

        public static void TryUnlock_Exclusive_SecondBlocked() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 200);
            s.Add(ResourceKind.Chitin, 200);
            s.Add(ResourceKind.Ember, 200);
            var known = new System.Collections.Generic.List<string> { "spore", "spikeback", "salamander" };
            var mut = new MutationSet();
            Check.True(e.TryUnlock("hunt_root", s, known, mut), "unlock hunt_root");
            Check.True(e.TryUnlock("hunt_spikes", s, known, mut), "unlock hunt_spikes");
            // hunt_vampire в той же группе — блокируется
            Check.True(!e.TryUnlock("hunt_vampire", s, known, mut), "hunt_vampire blocked");
            Check.True(!e.Unlocked.Contains("hunt_vampire"), "not marked");
        }

        public static void TryUnlock_GeneLocked_WithoutKnownSpecies() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Add(ResourceKind.Psyche, 50);
            s.Add(ResourceKind.Crystal, 50);
            var known = new System.Collections.Generic.List<string> { "spore" }; // нет glowworm
            var mut = new MutationSet();
            Check.True(!e.TryUnlock("senses_eyes", s, known, mut), "gene locked");
            Check.Eq(mut.Stacks("m_eyes"), 0, "m_eyes not added");
        }

        public static void Sum_AggregatesAcrossNodes() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "body_bulwark", "body_kevlar" });
            // DamageTakenMult: -0.15 + -0.25 + -0.30 = -0.70
            Check.Eq(e.Sum(EvoEffectKind.DamageTakenMult), -0.70, "sum DamageTakenMult");
        }

        public static void Sum_NoNodes_Zero() {
            var e = new EvolutionState();
            Check.Eq(e.Sum(EvoEffectKind.MaxDash), 0.0, "empty sum == 0");
        }

        public static void Sum_MassGainMult_MultipleNodes() {
            var e = EvolutionState.FromArray(new[] { "body_mass", "body_adrenaline" });
            // 0.20 + 0.30 = 0.50
            Check.Eq(e.Sum(EvoEffectKind.MassGainMult), 0.50, "MassGainMult sum");
        }

        public static void ToArray_FromArray_RoundTrip() {
            var e = new EvolutionState();
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 500); s.Add(ResourceKind.Chitin, 500); s.Add(ResourceKind.Ember, 500);
            s.Add(ResourceKind.Slime, 500); s.Add(ResourceKind.Crystal, 500); s.Add(ResourceKind.Psyche, 500);
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            e.TryUnlock("body_thick_skin", s, known, mut);
            e.TryUnlock("body_mass", s, known, mut);
            string[] arr = e.ToArray();
            Check.Eq(arr.Length, 2, "ToArray length");
            var e2 = EvolutionState.FromArray(arr);
            Check.True(e2.Unlocked.Contains("body_thick_skin"), "FromArray has body_thick_skin");
            Check.True(e2.Unlocked.Contains("body_mass"), "FromArray has body_mass");
        }

        public static void FromArray_FiltersUnknown() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "fake_node", null });
            Check.Eq(e.Unlocked.Count, 1, "only known node kept");
            Check.True(e.Unlocked.Contains("body_thick_skin"), "kept body_thick_skin");
        }

        public static void FreeTokens_DefaultZero() {
            var e = new EvolutionState();
            Check.Eq(e.FreeTokens, 0, "FreeTokens default 0");
        }

        public static void CanUnlockFree_NoToken_NotEnoughResources() {
            var e = new EvolutionState();
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlockFree("body_thick_skin", known), UnlockBlock.NotEnoughResources, "no token");
        }

        public static void CanUnlockFree_WithToken_None() {
            var e = new EvolutionState { FreeTokens = 1 };
            var known = new System.Collections.Generic.List<string> { "spore" };
            Check.Eq(e.CanUnlockFree("body_thick_skin", known), UnlockBlock.None, "with token");
        }

        public static void TryUnlockFree_ConsumesTokenAndApplies() {
            var e = new EvolutionState { FreeTokens = 1 };
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            bool ok = e.TryUnlockFree("body_thick_skin", known, mut);
            Check.True(ok, "TryUnlockFree succeeds");
            Check.Eq(e.FreeTokens, 0, "token consumed");
            Check.True(e.Unlocked.Contains("body_thick_skin"), "marked unlocked");
        }

        public static void TryUnlockFree_DoesNotTouchStore() {
            var e = new EvolutionState { FreeTokens = 1 };
            var s = new ResourceStore();
            s.Add(ResourceKind.Organic, 5); // было бы мало для body_thick_skin (8)
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            bool ok = e.TryUnlockFree("body_thick_skin", known, mut);
            Check.True(ok, "TryUnlockFree ignores resources");
            Check.Eq(s.Get(ResourceKind.Organic), 5.0, "store untouched");
        }

        public static void TryUnlockFree_AppliesMutation() {
            var e = new EvolutionState { FreeTokens = 2 };
            var known = new System.Collections.Generic.List<string> { "spore", "glowworm" };
            var mut = new MutationSet();
            bool ok = e.TryUnlockFree("senses_eyes", known, mut);
            Check.True(ok, "TryUnlockFree senses_eyes");
            Check.Eq(mut.Stacks("m_eyes"), 1, "m_eyes added via free token");
        }

        public static void TryUnlockFree_BlockedByMissingPrereq() {
            var e = new EvolutionState { FreeTokens = 5 };
            var known = new System.Collections.Generic.List<string> { "spore" };
            var mut = new MutationSet();
            bool ok = e.TryUnlockFree("body_mass", known, mut); // требует body_thick_skin
            Check.True(!ok, "TryUnlockFree blocked by prereq");
            Check.Eq(e.FreeTokens, 5, "token not consumed on block");
        }

        public static void BlockText_AllCases() {
            Check.Eq(EvolutionState.BlockText(UnlockBlock.None),               "Доступно",                "None");
            Check.Eq(EvolutionState.BlockText(UnlockBlock.AlreadyUnlocked),    "Уже открыто",             "AlreadyUnlocked");
            Check.Eq(EvolutionState.BlockText(UnlockBlock.MissingPrereq),      "Нужен предыдущий узел",   "MissingPrereq");
            Check.Eq(EvolutionState.BlockText(UnlockBlock.ExclusiveTaken),     "Выбран другой путь",      "ExclusiveTaken");
            Check.Eq(EvolutionState.BlockText(UnlockBlock.GeneUnknown),        "Поглотите нужный вид",    "GeneUnknown");
            Check.Eq(EvolutionState.BlockText(UnlockBlock.NotEnoughResources), "Недостаточно ресурсов",   "NotEnoughResources");
        }

        public static void TryUnlock_ForkChoice_RespectsExclusive() {
            // Ветка Motion: dash2 vs crystal_charge — можно взять только один.
            var e = new EvolutionState();
            var s = new ResourceStore();
            for (int i = 0; i < ResourceInfo.Count; i++) s.Add((ResourceKind)i, 200);
            var known = new System.Collections.Generic.List<string> { "spore", "crystal_mite" };
            var mut = new MutationSet();
            Check.True(e.TryUnlock("motion_root", s, known, mut), "motion_root");
            Check.True(e.TryUnlock("motion_dash2", s, known, mut), "motion_dash2 chosen");
            // Теперь crystal_charge в той же exclusive-группе — недоступен
            Check.Eq(e.CanUnlock("motion_crystal_charge", s, known), UnlockBlock.ExclusiveTaken, "crystal_charge blocked");
        }

        public static void TryUnlock_FullChain_AppliesAllMutations() {
            // body_thick_skin -> body_chitin_plating (m_shell) -> body_bulwark
            var e = new EvolutionState();
            var s = new ResourceStore();
            for (int i = 0; i < ResourceInfo.Count; i++) s.Add((ResourceKind)i, 200);
            var known = new System.Collections.Generic.List<string> { "spore", "beetle" };
            var mut = new MutationSet();
            Check.True(e.TryUnlock("body_thick_skin", s, known, mut), "1");
            Check.True(e.TryUnlock("body_chitin_plating", s, known, mut), "2");
            Check.True(e.TryUnlock("body_bulwark", s, known, mut), "3");
            Check.Eq(mut.Stacks("m_shell"), 1, "m_shell applied once");
            Check.Eq(e.Sum(EvoEffectKind.DamageTakenMult), -0.15 + -0.25, "DamageTakenMult total");
        }
    }

    // ============================================================
    //  Показатели героя
    // ============================================================
    public static class EvoStatTests {
        private static MutationSet EmptyMut() => new MutationSet();
        private static EvolutionState EmptyEvo() => new EvolutionState();
        private static SoulMeta EmptySoul() => new SoulMeta();

        public static void Build_HasAll14Keys() {
            var lines = StatSheet.Build(7.5, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            Check.True(lines.Count >= 14, ">= 14 lines, got " + lines.Count);
            var keys = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < lines.Count; i++) keys.Add(lines[i].Key);
            string[] expected = { "mass","fragments","dash","dash_speed","damage","defense",
                                  "pull","light","regen","absorb","dash_cost","yield","storage","mass_gain" };
            for (int i = 0; i < expected.Length; i++) {
                Check.True(keys.Contains(expected[i]), "key " + expected[i] + " present");
            }
        }

        public static void Build_AllLinesHaveNonEmptyFields() {
            var lines = StatSheet.Build(7.5, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            for (int i = 0; i < lines.Count; i++) {
                var l = lines[i];
                Check.True(l.Key != null && l.Key.Length > 0, "Key " + i);
                Check.True(l.Name != null && l.Name.Length > 0, "Name " + i);
                Check.True(l.Value != null && l.Value.Length > 0, "Value " + i);
                Check.True(l.Tooltip != null && l.Tooltip.Length > 0, "Tooltip " + i);
                Check.True(l.Sources != null && l.Sources.Length > 0, "Sources " + i);
            }
        }

        public static void Mass_FormulaAndValue() {
            var lines = StatSheet.Build(7.4, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var mass = Find(lines, "mass");
            Check.Eq(mass.Raw, 7.4, "mass raw");
            Check.Eq(mass.Value, "7.4", "mass value");
        }

        public static void Fragments_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var fr = Find(lines, "fragments");
            Check.Eq(fr.Raw, 1.0, "one body until Деление");
            Check.Eq(fr.Value, "0 / 1", "fragments value");
        }

        public static void Fragments_WithEvoMaxFragments() {
            var e = EvolutionState.FromArray(new[] { "div_split_basics", "div_triplet", "div_swarm" });
            var lines = StatSheet.Build(5.0, 3, 1, EmptyMut(), e, EmptySoul());
            var fr = Find(lines, "fragments");
            // 2 + 1 + 2 = 5
            Check.Eq(fr.Raw, 5.0, "maxFragments with swarm");
            Check.Eq(fr.Value, "3 / 5", "fragments value with swarm");
        }

        public static void Dash_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var dash = Find(lines, "dash");
            // m.DashCharges=1 + meta.BonusDashCharges=0 + e.Sum(MaxDash)=0 = 1
            Check.Eq(dash.Raw, 1.0, "maxDash default 1");
            Check.Eq(dash.Value, "1 / 1", "dash value");
        }

        public static void Dash_WithMutationDash2() {
            var mut = MutationSet.FromArray(new[] { "m_dash2" });
            var lines = StatSheet.Build(5.0, 0, 2, mut, EmptyEvo(), EmptySoul());
            var dash = Find(lines, "dash");
            Check.Eq(dash.Raw, 2.0, "maxDash with m_dash2");
        }

        public static void DashSpeed_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var ds = Find(lines, "dash_speed");
            // 1.0 * (1 + 0) = 1.0
            Check.Eq(ds.Raw, 1.0, "dashSpeed default 1.0");
        }

        public static void DashSpeed_WithEvoDashSpeed() {
            var e = EvolutionState.FromArray(new[] { "motion_root", "motion_dash2", "motion_dash_speed" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var ds = Find(lines, "dash_speed");
            // 1.0 * (1 + 0.10 + 0.20) = 1.30
            Check.Eq(ds.Raw, 1.30, "dashSpeed with motion_root+dash_speed");
        }

        public static void Damage_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var dmg = Find(lines, "damage");
            Check.Eq(dmg.Raw, 1.0, "damage default 1.0");
        }

        public static void Damage_WithSpikes() {
            var mut = MutationSet.FromArray(new[] { "m_spikes", "m_spikes" }); // 2 стака
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var dmg = Find(lines, "damage");
            // 1.5^2 = 2.25
            Check.Eq(dmg.Raw, 2.25, "damage with 2 spikes");
        }

        public static void Defense_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var def = Find(lines, "defense");
            // 1.0 * (1 + 0) = 1.0
            Check.Eq(def.Raw, 1.0, "defense default 1.0");
        }

        public static void Defense_WithShellAndEvo() {
            var mut = MutationSet.FromArray(new[] { "m_shell" }); // 0.7
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "body_chitin_plating", "body_bulwark" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, e, EmptySoul());
            var def = Find(lines, "defense");
            // 0.7 * (1 + -0.15 + -0.25) = 0.7 * 0.60 = 0.42
            Check.Eq(def.Raw, 0.42, "defense with shell + evo");
        }

        public static void Pull_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var pull = Find(lines, "pull");
            Check.Eq(pull.Raw, 1.0, "pull default 1.0");
        }

        public static void Pull_WithMagnetAndEvo() {
            var mut = MutationSet.FromArray(new[] { "m_magnet" }); // 1.5
            var e = EvolutionState.FromArray(new[] { "senses_eyes", "senses_magnet", "senses_pull" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, e, EmptySoul());
            var pull = Find(lines, "pull");
            // 1.5 + 2 = 3.5
            Check.Eq(pull.Raw, 3.5, "pull with magnet + evo");
        }

        public static void Light_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var light = Find(lines, "light");
            Check.Eq(light.Raw, 3.5, "light default 3.5");
        }

        public static void Light_WithEyesAndEvo() {
            var mut = MutationSet.FromArray(new[] { "m_eyes" }); // +2
            var e = EvolutionState.FromArray(new[] { "senses_eyes", "senses_perception" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, e, EmptySoul());
            var light = Find(lines, "light");
            // 3.5 + 2 (eyes) + 2 (perception) = 7.5
            Check.Eq(light.Raw, 7.5, "light with eyes + perception");
        }

        public static void Regen_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var r = Find(lines, "regen");
            Check.Eq(r.Raw, 0.0, "regen default 0");
        }

        public static void Regen_WithRegrow() {
            var mut = MutationSet.FromArray(new[] { "m_regrow", "m_regrow" }); // 0.04
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var r = Find(lines, "regen");
            Check.Eq(r.Raw, 0.04, "regen with 2 regrow");
        }

        public static void Absorb_Formula_Default() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var a = Find(lines, "absorb");
            Check.Eq(a.Raw, 1.25, "absorb default 1.25");
        }

        public static void Absorb_WithHunger() {
            var mut = MutationSet.FromArray(new[] { "m_hunger" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var a = Find(lines, "absorb");
            Check.Eq(a.Raw, 1.10, "absorb with hunger 1.10");
        }

        public static void DashCost_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var dc = Find(lines, "dash_cost");
            Check.Eq(dc.Raw, 1.0, "dash_cost default 1.0");
        }

        public static void DashCost_WithLight() {
            var mut = MutationSet.FromArray(new[] { "m_light" }); // 0.6
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var dc = Find(lines, "dash_cost");
            Check.Eq(dc.Raw, 0.6, "dash_cost with m_light");
        }

        public static void Yield_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var y = Find(lines, "yield");
            Check.Eq(y.Raw, 1.0, "yield default 1.0");
        }

        public static void Yield_WithHarvester() {
            var e = EvolutionState.FromArray(new[] { "div_split_basics", "div_storage", "div_harvester" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var y = Find(lines, "yield");
            // 1 + 0.25 = 1.25
            Check.Eq(y.Raw, 1.25, "yield with harvester");
        }

        public static void Storage_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var s = Find(lines, "storage");
            Check.Eq(s.Raw, 40.0, "storage default 40");
        }

        public static void Storage_WithEvoStorage() {
            var e = EvolutionState.FromArray(new[] { "div_split_basics", "div_storage", "div_bottomless" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var s = Find(lines, "storage");
            // 40 + 20 + 40 = 100
            Check.Eq(s.Raw, 100.0, "storage with evo");
        }

        public static void MassGain_Formula() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var mg = Find(lines, "mass_gain");
            Check.Eq(mg.Raw, 1.0, "mass_gain default 1.0");
        }

        public static void MassGain_WithEvo() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "body_mass", "body_adrenaline" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var mg = Find(lines, "mass_gain");
            // 1 + 0.20 + 0.30 = 1.50
            Check.Eq(mg.Raw, 1.50, "mass_gain with body_mass+adrenaline");
        }

        public static void Sources_IncludeBase() {
            var lines = StatSheet.Build(7.5, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            // Все строки должны иметь хотя бы один Source
            for (int i = 0; i < lines.Count; i++) {
                Check.True(lines[i].Sources.Length >= 1, lines[i].Key + " has >= 1 source");
            }
        }

        public static void Sources_Storage_IncludeEvoNode() {
            var e = EvolutionState.FromArray(new[] { "div_split_basics", "div_storage" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var s = Find(lines, "storage");
            bool foundKladovaya = false;
            for (int i = 0; i < s.Sources.Length; i++) {
                if (s.Sources[i].IndexOf("Кладовая") >= 0) { foundKladovaya = true; break; }
            }
            Check.True(foundKladovaya, "storage sources include Кладовая");
        }

        public static void Sources_Dash_IncludeMutationSource() {
            var mut = MutationSet.FromArray(new[] { "m_dash2" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var dash = Find(lines, "dash");
            bool foundDash2 = false;
            for (int i = 0; i < dash.Sources.Length; i++) {
                if (dash.Sources[i].IndexOf("Второй рывок") >= 0) { foundDash2 = true; break; }
            }
            Check.True(foundDash2, "dash sources include Второй рывок");
        }

        public static void Sources_Defense_IncludeEvoNode() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var def = Find(lines, "defense");
            bool foundThick = false;
            for (int i = 0; i < def.Sources.Length; i++) {
                if (def.Sources[i].IndexOf("Густая кожа") >= 0) { foundThick = true; break; }
            }
            Check.True(foundThick, "defense sources include Густая кожа");
        }

        public static void Sources_Light_IncludeEyesSource() {
            var mut = MutationSet.FromArray(new[] { "m_eyes" });
            var lines = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            var light = Find(lines, "light");
            bool foundEyes = false;
            for (int i = 0; i < light.Sources.Length; i++) {
                if (light.Sources[i].IndexOf("Глаза") >= 0) { foundEyes = true; break; }
            }
            Check.True(foundEyes, "light sources include Глаза");
        }

        public static void Sources_MassGain_IncludeEvoNode() {
            var e = EvolutionState.FromArray(new[] { "body_thick_skin", "body_mass" });
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), e, EmptySoul());
            var mg = Find(lines, "mass_gain");
            bool foundMass = false;
            for (int i = 0; i < mg.Sources.Length; i++) {
                if (mg.Sources[i].IndexOf("Накопление массы") >= 0) { foundMass = true; break; }
            }
            Check.True(foundMass, "mass_gain sources include Накопление массы");
        }

        public static void Sources_Dash_ExcludesSoul_WhenZeroBonus() {
            // С дефолтной SoulMeta BonusDashCharges=0 → "Душа" в sources не упоминается
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var dash = Find(lines, "dash");
            for (int i = 0; i < dash.Sources.Length; i++) {
                Check.True(dash.Sources[i].IndexOf("Душа") < 0, "no soul source when BonusDashCharges=0");
            }
        }

        public static void Sources_DashCost_ExcludesSoul_WhenMult1() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            var dc = Find(lines, "dash_cost");
            for (int i = 0; i < dc.Sources.Length; i++) {
                Check.True(dc.Sources[i].IndexOf("Душа") < 0, "no soul source when DashCostMult=1.0");
            }
        }

        public static void Value_Defense_FormattedAsPercent() {
            // defense = 1.0 → "0%"; defense = 0.7 → "−30%" (U+2212)
            var lines1 = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            Check.Eq(Find(lines1, "defense").Value, "0%", "defense 1.0 -> 0%");

            var mut = MutationSet.FromArray(new[] { "m_shell" }); // 0.7
            var lines2 = StatSheet.Build(5.0, 0, 1, mut, EmptyEvo(), EmptySoul());
            Check.Eq(Find(lines2, "defense").Value, "−30%", "defense 0.7 -> −30%");
        }

        public static void Value_Multipliers_HaveXPrefix() {
            var lines = StatSheet.Build(5.0, 0, 1, EmptyMut(), EmptyEvo(), EmptySoul());
            // dash_speed, damage, absorb, dash_cost, yield, mass_gain — все начинаются с "×"
            string[] multKeys = { "dash_speed", "damage", "absorb", "dash_cost", "yield", "mass_gain" };
            for (int i = 0; i < multKeys.Length; i++) {
                var l = Find(lines, multKeys[i]);
                Check.True(l.Value.StartsWith("×"), multKeys[i] + " starts with ×, got " + l.Value);
            }
        }

        private static StatLine Find(System.Collections.Generic.List<StatLine> lines, string key) {
            for (int i = 0; i < lines.Count; i++) if (lines[i].Key == key) return lines[i];
            return default;
        }
    }
}
