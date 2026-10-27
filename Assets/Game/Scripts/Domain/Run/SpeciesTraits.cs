namespace PM.Domain.Run {

/// <summary>Особые черты вида поверх базового описания каталога.</summary>
public struct SpeciesTraits {
    /// <summary>Удары медленнее этого порога не ранят существо (броня); 0 — брони нет.</summary>
    public double ArmorSpeed;
    /// <summary>При уроне существо делится на 2 половины (если масса ≥ 1.5).</summary>
    public bool SplitsOnHit;
    /// <summary>Пока касается игрока, тянет массу (масс/сек).</summary>
    public double LeechPerSec;
    /// <summary>Светится сквозь тьму.</summary>
    public bool Glows;
}

/// <summary>Черты видов по id. Детерминирован, без LINQ.</summary>
public static class SpeciesTraitsCatalog {
    /// <summary>Черты вида; неизвестный/пустой id — нейтральные черты.</summary>
    public static SpeciesTraits Get(string speciesId) {
        var t = new SpeciesTraits();
        switch (speciesId) {
            case "shard_crab": t.ArmorSpeed = 4.0; break;
            case "crystal_mite": t.ArmorSpeed = 3.0; break;
            case "warden": t.ArmorSpeed = 6.0; break;
            case "bubble_jelly": t.SplitsOnHit = true; break;
            case "leech": t.LeechPerSec = 0.25; break;
            case "deep_shark": t.ArmorSpeed = 2.5; break; // v0.15: элитная акула гасит медленные удары
        }
        if (speciesId == "glowworm" || speciesId == "wisp"
            || speciesId == "crystal_mite" || speciesId == "ember_beetle") t.Glows = true;
        return t;
    }
}
}
