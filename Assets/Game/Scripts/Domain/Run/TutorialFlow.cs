// PM.Domain.Run — обучение первой жизни (v0.10). C# 9, без LINQ и без UnityEngine.
// Шаги идут по порядку; каждый закрывается событием из игры. Unity только сообщает события
// и рисует подсказку/указатель — правила прохождения проверяются тестами.
namespace PM.Domain.Run {
    public enum TutorialStep {
        Move = 0,        // подвигаться
        EatSpores = 1,   // съесть несколько спор
        Rings = 2,       // съесть существо с зелёным кольцом
        Dash = 3,        // сделать рывок
        Evolve = 4,      // открыть «Эволюцию», когда есть жетон
        Split = 5,       // развить «Деление» и разделиться
        Gather = 6,      // собрать клонов обратно
        Goal = 7,        // объяснить цель и попрощаться
        Done = 8
    }

    public sealed class TutorialFlow {
        public const double MoveDistance = 4.0;
        public const int SporesToEat = 3;
        public const double GoalShowSeconds = 6.0;
        public const double SoftTimeout = 40.0;   // шаг не даётся — через 40 с подсказка уступает игре

        public TutorialStep Step { get; private set; }
        public bool Active => Step != TutorialStep.Done;
        public int Index => (int)Step;
        public static int Count => (int)TutorialStep.Done;

        private double _moved, _stepTime;
        private int _spores;

        public TutorialFlow(bool alreadyDone = false) { Step = alreadyDone ? TutorialStep.Done : TutorialStep.Move; }

        public void Skip() { Step = TutorialStep.Done; }

        // ---------- события игры ----------
        public void OnMoved(double distance) {
            if (Step != TutorialStep.Move || distance <= 0) return;
            _moved += distance;
            if (_moved >= MoveDistance) Next();
        }

        public void OnAbsorb(bool food) {
            if (Step == TutorialStep.EatSpores && food && ++_spores >= SporesToEat) Next();
            else if (Step == TutorialStep.Rings && !food) Next();
        }

        public void OnDash() { if (Step == TutorialStep.Dash) Next(); }

        /// <summary>Шаг «Эволюция» начинается, только когда есть чем платить — жетон или ресурсы на «Деление».</summary>
        public void OnEvolutionOpened() { if (Step == TutorialStep.Evolve) Next(); }

        public void OnSplit() { if (Step == TutorialStep.Split) Next(); }

        public void OnMerged() { if (Step == TutorialStep.Gather) Next(); }

        /// <summary>
        /// Время идёт: «Цель» закрывается сама; затянувшийся шаг (не нашлось существа, нет жетона)
        /// не держит игрока — переходим дальше. canEvolve — у игрока есть жетон или «Деление» по карману.
        /// </summary>
        public void Tick(double dt, bool canEvolve) {
            if (!Active || dt <= 0) return;
            if (Step == TutorialStep.Evolve && !canEvolve) return;      // ждём первого жетона, не торопим
            _stepTime += dt;
            if (Step == TutorialStep.Goal && _stepTime >= GoalShowSeconds) { Next(); return; }
            if (_stepTime >= SoftTimeout && Step != TutorialStep.Evolve) Next();
        }

        private void Next() {
            Step = Step + 1;
            _stepTime = 0;
        }

        /// <summary>Текст шага; joystick — режим управления (подсказка про движение разная).</summary>
        public static string Text(TutorialStep s, bool joystick) {
            switch (s) {
                case TutorialStep.Move: return joystick
                    ? "Ведите пальцем по джойстику слева — капля поползёт"
                    : "Коснитесь экрана, оттяните палец ПРОТИВ направления и отпустите";
                case TutorialStep.EatSpores: return "Съешьте " + SporesToEat + " светящиеся споры — это масса и ресурсы";
                case TutorialStep.Rings: return "Кольцо вокруг существа: зелёное — можно съесть, красное — беги! Съешьте зелёного";
                case TutorialStep.Dash: return joystick
                    ? "Рывок — большая кнопка справа: быстрый бросок, пробивает броню"
                    : "Рывок — сильнее оттяните и отпустите: скорость пробивает броню";
                case TutorialStep.Evolve: return "Есть жетон эволюции! Откройте «Эволюцию» — круглая кнопка слева";
                case TutorialStep.Split: return "В ветке «Деление» откройте «Деление», затем нажмите кнопку «Деление» справа";
                case TutorialStep.Gather: return "Клоны охотятся сами и несут добычу. Нажмите «Собраться», чтобы сдать её";
                case TutorialStep.Goal: return "Цель: наберите массу — у шахты проснётся страж. Победите его и спускайтесь!";
            }
            return "";
        }
    }
}
