// PM.Domain.Run — состояние дерева эволюции героя (v0.8). C# 9, без LINQ.
using System.Collections.Generic;

namespace PM.Domain.Run {
    public enum UnlockBlock {
        None = 0,
        AlreadyUnlocked = 1,
        MissingPrereq = 2,
        ExclusiveTaken = 3,
        GeneUnknown = 4,
        NotEnoughResources = 5
    }

    public sealed class EvolutionState {
        private readonly System.Collections.Generic.HashSet<string> _unlocked = new System.Collections.Generic.HashSet<string>();

        public HashSet<string> Unlocked => _unlocked;
        public int FreeTokens { get; set; }

        public UnlockBlock CanUnlock(string id, ResourceStore store, ICollection<string> knownSpecies) {
            var node = EvolutionTree.Get(id);
            if (node == null) return UnlockBlock.MissingPrereq;
            if (_unlocked.Contains(id)) return UnlockBlock.AlreadyUnlocked;
            if (!HasPrereqs(node)) return UnlockBlock.MissingPrereq;
            if (ExclusiveTaken(node)) return UnlockBlock.ExclusiveTaken;
            if (!GeneKnown(node, knownSpecies)) return UnlockBlock.GeneUnknown;
            if (store == null || !store.CanPay(node.Cost)) return UnlockBlock.NotEnoughResources;
            return UnlockBlock.None;
        }

        public UnlockBlock CanUnlockFree(string id, ICollection<string> knownSpecies) {
            var node = EvolutionTree.Get(id);
            if (node == null) return UnlockBlock.MissingPrereq;
            if (_unlocked.Contains(id)) return UnlockBlock.AlreadyUnlocked;
            if (FreeTokens <= 0) return UnlockBlock.NotEnoughResources;
            if (!HasPrereqs(node)) return UnlockBlock.MissingPrereq;
            if (ExclusiveTaken(node)) return UnlockBlock.ExclusiveTaken;
            if (!GeneKnown(node, knownSpecies)) return UnlockBlock.GeneUnknown;
            return UnlockBlock.None;
        }

        public bool TryUnlock(string id, ResourceStore store, ICollection<string> knownSpecies, MutationSet mutations) {
            if (CanUnlock(id, store, knownSpecies) != UnlockBlock.None) return false;
            var node = EvolutionTree.Get(id);
            store.Pay(node.Cost);
            ApplyEffects(node, mutations);
            _unlocked.Add(id);
            return true;
        }

        public bool TryUnlockFree(string id, ICollection<string> knownSpecies, MutationSet mutations) {
            if (CanUnlockFree(id, knownSpecies) != UnlockBlock.None) return false;
            var node = EvolutionTree.Get(id);
            FreeTokens -= 1;
            ApplyEffects(node, mutations);
            _unlocked.Add(id);
            return true;
        }

        public double Sum(EvoEffectKind k) {
            double total = 0;
            foreach (var id in _unlocked) {
                var node = EvolutionTree.Get(id);
                if (node == null || node.Effects == null) continue;
                for (int i = 0; i < node.Effects.Length; i++) {
                    if (node.Effects[i].Kind == k) total += node.Effects[i].Value;
                }
            }
            return total;
        }

        public string[] ToArray() {
            var arr = new string[_unlocked.Count];
            int i = 0;
            foreach (var id in _unlocked) arr[i++] = id;
            return arr;
        }

        public static EvolutionState FromArray(string[] ids) {
            var s = new EvolutionState();
            if (ids != null) {
                for (int i = 0; i < ids.Length; i++) {
                    if (ids[i] != null && EvolutionTree.Get(ids[i]) != null)
                        s._unlocked.Add(ids[i]);
                }
            }
            return s;
        }

        public static string BlockText(UnlockBlock b) {
            switch (b) {
                case UnlockBlock.None:               return "Доступно";
                case UnlockBlock.AlreadyUnlocked:    return "Уже открыто";
                case UnlockBlock.MissingPrereq:      return "Нужен предыдущий узел";
                case UnlockBlock.ExclusiveTaken:     return "Выбран другой путь";
                case UnlockBlock.GeneUnknown:        return "Поглотите нужный вид";
                case UnlockBlock.NotEnoughResources: return "Недостаточно ресурсов";
            }
            return b.ToString();
        }

        private bool HasPrereqs(EvoNode node) {
            if (node.Requires == null) return true;
            for (int i = 0; i < node.Requires.Length; i++) {
                if (!_unlocked.Contains(node.Requires[i])) return false;
            }
            return true;
        }

        private bool ExclusiveTaken(EvoNode node) {
            if (node.ExclusiveGroup == null) return false;
            var all = EvolutionTree.All();
            for (int i = 0; i < all.Length; i++) {
                if (all[i].ExclusiveGroup == node.ExclusiveGroup && _unlocked.Contains(all[i].Id))
                    return true;
            }
            return false;
        }

        private static bool GeneKnown(EvoNode node, ICollection<string> knownSpecies) {
            if (node.RequiredGene == null) return true;
            if (knownSpecies == null) return false;
            foreach (var s in knownSpecies) {
                if (s == node.RequiredGene) return true;
            }
            return false;
        }

        private static void ApplyEffects(EvoNode node, MutationSet mutations) {
            if (node.Effects == null || mutations == null) return;
            for (int i = 0; i < node.Effects.Length; i++) {
                if (node.Effects[i].Kind == EvoEffectKind.Mutation
                    && node.Effects[i].MutationId != null
                    && mutations.CanAdd(node.Effects[i].MutationId)) {
                    mutations.Add(node.Effects[i].MutationId);
                }
            }
        }
    }
}
