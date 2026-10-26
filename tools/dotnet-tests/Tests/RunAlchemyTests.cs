using PM.Domain.Run;
using static PMHarness.Check;
namespace PMHarness {

public static class RunAlchemyTests {

    // ---------- базовые отказы ----------

    public static void TryTransmute_RejectsInNormalRoom() {
        var st = new RunState(new SoulMeta(), 3UL);
        // Первая комната — Normal, не Shrine
        Eq(RoomKind.Normal, st.Layout.Kind, "первая комната — Normal");
        True(!st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "в Normal алхимии нет");
        Eq(0, st.TransmutesLeft, "трансмутаций 0 — не Shrine");
        True(st.LastTransmute == null, "LastTransmute не тронут");
    }

    public static void TryTransmute_RejectsSameFromTo() {
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine-комната найдена");
        // Добавим немного ресурсов, чтобы отказа не было из-за их нехватки.
        st.Resources.Add(ResourceKind.Organic, 10);
        True(!st.TryTransmute(ResourceKind.Organic, ResourceKind.Organic),
            "нельзя конвертировать ресурс сам в себя");
        True(st.LastTransmute == null, "LastTransmute не тронут");
    }

    public static void TryTransmute_RejectsIfNotEnoughResources() {
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        // Хранилище пусто — Chitin нет.
        Eq(0.0, st.Resources.Get(ResourceKind.Chitin), "Chitin: 0");
        True(!st.TryTransmute(ResourceKind.Chitin, ResourceKind.Psyche),
            "нужно 3 Chitin, а их 0");
        True(st.LastTransmute == null, "LastTransmute не тронут");
    }

    // ---------- успешная конвертация ----------

    public static void TryTransmute_ConvertsThreeToOne() {
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        st.Resources.Add(ResourceKind.Chitin, 10);
        st.Resources.Add(ResourceKind.Psyche, 5);
        double chitinBefore = st.Resources.Get(ResourceKind.Chitin);
        double psycheBefore = st.Resources.Get(ResourceKind.Psyche);
        True(st.TryTransmute(ResourceKind.Chitin, ResourceKind.Psyche), "трансмутация успешна");
        Eq(chitinBefore - 3.0, st.Resources.Get(ResourceKind.Chitin), "Chitin −3");
        Eq(psycheBefore + 1.0, st.Resources.Get(ResourceKind.Psyche), "Psyche +1");
        True(st.LastTransmute != null, "LastTransmute заполнен");
        Eq(ResourceKind.Chitin, st.LastTransmute.From, "From = Chitin");
        Eq(ResourceKind.Psyche, st.LastTransmute.To, "To = Psyche");
        Eq(RunState.MaxTransmutesPerShrine - 1, st.TransmutesLeft, "осталось 2 трансмутации");
    }

    public static void TryTransmute_LimitedToThreePerShrine() {
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        // Запасаемся Organic и делаем 3 трансмутации в Psyche.
        st.Resources.Add(ResourceKind.Organic, 30);
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "1-я трансмутация");
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "2-я трансмутация");
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "3-я трансмутация");
        Eq(0, st.TransmutesLeft, "лимит исчерпан");
        True(!st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "4-я — отказ");
        // Хватает ресурсов, но лимит — 3 за комнату.
        True(st.Resources.Get(ResourceKind.Organic) >= 3.0, "ресурсов ещё полно");
    }

    public static void TryTransmute_AllResourcesConvertible() {
        // Проверяем все 6×5 пар направлений (кроме самопересечений): конвертация идёт.
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        // Снимаем лимит, переходя в новую Shrine-комнату для каждой пары.
        for (int fromI = 0; fromI < 6; fromI++)
        for (int toI = 0; toI < 6; toI++) {
            if (fromI == toI) continue;
            var shrine = FindShrineAtSecondRoom(201 + fromI * 6 + toI);
            if (shrine == null) continue;
            shrine.Resources.Add((ResourceKind)fromI, 10);
            shrine.Resources.Add((ResourceKind)toI, 0);
            True(shrine.TryTransmute((ResourceKind)fromI, (ResourceKind)toI),
                "конвертация " + fromI + "→" + toI + " должна работать");
        }
    }

    public static void TryTransmute_ResetBetweenRooms() {
        // Находим Shrine, тратим 2 трансмутации, выходим в новую комнату — лимит должен восстановиться.
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        st.Resources.Add(ResourceKind.Organic, 20);
        st.Resources.Add(ResourceKind.Psyche, 5);
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "1-я");
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "2-я");
        Eq(RunState.MaxTransmutesPerShrine - 2, st.TransmutesLeft, "осталось 1");
        // Поглощаем цель и выходим в следующую комнату.
        st.OnAbsorb("spore", st.Room.GoalMass);
        True(st.TryEnterExit(), "выход из Shrine");
        // В новой комнате (Normal или иначе) — счётчик сброшен.
        Eq(0, st.TransmutesUsedThisRoom, "счётчик обнулён после перехода");
        True(st.LastTransmute == null, "LastTransmute сброшен");
    }

    public static void TryTransmute_WorksWithoutUseAltar() {
        // Алхимия — отдельная опция; не требует, чтобы алтарь был использован.
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        // Снимаем пороговые предложения, накопившиеся после поглощения спор в первой комнате.
        while (st.PendingOffer != null && st.PendingOffer.Options.Length > 0)
            st.ChooseMutation(st.PendingOffer.Options[0]);
        True(st.PendingOffer == null || st.PendingOffer.Options.Length == 0,
            "пороговые предложения исчерпаны");
        True(!st.AltarUsed, "алтарь ещё не использован");
        st.Resources.Add(ResourceKind.Organic, 5);
        True(st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche), "алхимия работает без дара");
        True(!st.AltarUsed, "алтарь по-прежнему не тронут");
        True(st.PendingOffer == null || st.PendingOffer.Options.Length == 0,
            "PendingOffer не появился от алхимии");
        st.UseAltar();
        True(st.AltarUsed, "теперь алтарь использован");
        True(st.PendingOffer != null && st.PendingOffer.Options.Length >= 1, "дар выдан после UseAltar");
    }

    public static void TryTransmute_RejectsWhenCapacityFull() {
        // Если целевой ресурс уже на пределе вместимости — трансмутация невозможна.
        var st = FindShrineAtSecondRoom();
        True(st != null, "Shrine найдена");
        // Заполняем Psyche до вместимости (40 по умолчанию).
        double cap = st.Resources.Capacity;
        while (st.Resources.Get(ResourceKind.Psyche) < cap) st.Resources.Add(ResourceKind.Psyche, 1.0);
        Eq(cap, st.Resources.Get(ResourceKind.Psyche), "Psyche на пределе");
        st.Resources.Add(ResourceKind.Organic, 10);
        double organicBeforeTransmute = st.Resources.Get(ResourceKind.Organic);
        True(organicBeforeTransmute >= 10.0, "Organic добавлен: " + organicBeforeTransmute);
        True(!st.TryTransmute(ResourceKind.Organic, ResourceKind.Psyche),
            "некуда положить Psyche — отказ");
        Eq(organicBeforeTransmute, st.Resources.Get(ResourceKind.Organic),
            "Organic не списан — отказ атомарен");
    }

    // ---------- helpers ----------

    /// <summary>Подбирает seed, у которого вторая комната — Shrine.</summary>
    private static RunState FindShrineAtSecondRoom(int startSeed = 1) {
        for (ulong seed = (ulong)startSeed; seed <= (ulong)(startSeed + 300); seed++) {
            var st = new RunState(new SoulMeta(), seed);
            st.OnAbsorb("spore", st.Room.GoalMass);
            if (!st.TryEnterExit()) continue;
            if (st.Layout.Kind == RoomKind.Shrine) return st;
        }
        return null;
    }
}
}
