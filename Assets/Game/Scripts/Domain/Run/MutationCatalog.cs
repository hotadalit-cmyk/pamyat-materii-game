namespace PM.Domain.Run {

/// <summary>Описание мутации (гена) тела.</summary>
public sealed class MutationDef {
    public string Id, Name, Description;
    public int MaxStacks;
}

/// <summary>Каталог мутаций забега (RUN_SPEC v0.4). Без LINQ.</summary>
public static class MutationCatalog {
    private static readonly MutationDef[] AllDefs = new MutationDef[] {
        new MutationDef { Id = "m_spikes",  Name = "Шипы",          Description = "Урон, наносимый ударом ×1.5 за стак",            MaxStacks = 2 },
        new MutationDef { Id = "m_shell",   Name = "Панцирь",       Description = "Получаемый урон ×0.7 за стак",                     MaxStacks = 2 },
        new MutationDef { Id = "m_sticky",  Name = "Липкость",      Description = "Упругость отскока −0.25 за стак",                 MaxStacks = 2 },
        new MutationDef { Id = "m_dash2",   Name = "Второй рывок",  Description = "+1 заряд рывка",                                   MaxStacks = 2 },
        new MutationDef { Id = "m_magnet",  Name = "Магнит",        Description = "Радиус притяжения ×1.5 за стак",                  MaxStacks = 2 },
        new MutationDef { Id = "m_eyes",    Name = "Глаза",         Description = "Радиус света +2.0 за стак",                       MaxStacks = 3 },
        new MutationDef { Id = "m_toxic",   Name = "Ядовитый след", Description = "След наносит 0.4 массы/сек врагам",                MaxStacks = 1 },
        new MutationDef { Id = "m_light",   Name = "Лёгкость",      Description = "Стоимость рывка ×0.6 за стак",                    MaxStacks = 2 },
        new MutationDef { Id = "m_regrow",  Name = "Регенерация",   Description = "+0.02 массы/сек за стак",                         MaxStacks = 2 },
        new MutationDef { Id = "m_hunger",  Name = "Голод",         Description = "Порог поглощения 1.25 → 1.10",                    MaxStacks = 1 },
        new MutationDef { Id = "m_crystal", Name = "Кристалл",      Description = "+25% массы мгновенно, но скорость рывка ×0.85",  MaxStacks = 1 },
        // v0.5
        new MutationDef { Id = "m_heatproof", Name = "Жаростойкость", Description = "Шипы, жар и испарение вредят ×0.4",              MaxStacks = 1 },
        new MutationDef { Id = "m_vampire",   Name = "Вампиризм",     Description = "35% нанесённого урона возвращается массой",      MaxStacks = 1 },
        new MutationDef { Id = "m_ricochet",  Name = "Рикошет",       Description = "+25% урона за каждый отскок в текущем рывке",   MaxStacks = 2 },
        new MutationDef { Id = "m_echo",      Name = "Эхолокация",    Description = "Существа вне света видны как метки (радиус 9)",  MaxStacks = 1 },
    };

    /// <summary>Все мутации. Возвращаемое значение считать read-only.</summary>
    public static MutationDef[] All() => AllDefs;

    /// <summary>Мутация по id; null, если нет.</summary>
    public static MutationDef Get(string id) {
        if (id == null) return null;
        for (int i = 0; i < AllDefs.Length; i++)
            if (AllDefs[i].Id == id) return AllDefs[i];
        return null;
    }
}
}
