Задача v0.15 — разнообразные и красивые уровни: подключить новый арт и модели, оживить биомы. v0.14 принята (влита, на телефоне массы в коридоре).

## База
`git checkout zai/v14 && git checkout -b zai/v15`. Правила прежние. Unity компилирую я; следи за using и Vec2/Vector2.
Мои правки после v0.14 у меня уже есть, их НЕ повторяй: импортёр текстур (Editor/ThirdPartyImport.cs — не трогай) и ассеты.
Ассетов в твоём репозитории НЕТ (бинарники добавил я) — пиши код против путей ниже, всегда с проверкой на null и запасным
вариантом (если Resources.Load вернул null — старое поведение).

## Ассеты (Resources-пути, без расширения)
- Пол биомов, 1024², плитка (Repeat): `AiArt/Floors/floor_b1_mushroom`, `floor_b2_ember`, `floor_b3_flooded`, `floor_b4_crystal`, `floor_b5_heart`.
  Они яркие и пёстрые — накладывать притушенно (яркость ~25–35%, умножение на цвет пола региона), масштаб плитки ~6–8 ед. мира.
- Пропсы, 512² RGBA, вид сверху/¾: `AiArt/Props/<имя>`:
  b1: b1_p1_mushroom_cluster, b1_p2_mushroom_trio, b1_p3_glow_moss, b1_p4_spore_puffball, b1_p5_glow_fern, b1_p6_mossy_rock;
  b2: b2_p1_lava_crack, b2_p2_ember_mound, b2_p3_obsidian_rocks, b2_p4_steam_vent, b2_p5_lava_bubble, b2_p6_charred_rock;
  b3: b3_p1_lily_pad, b3_p2_glow_algae, b3_p3_glow_coral, b3_p4_wet_rock, b3_p5_small_jellyfish, b3_p6_bubbles_reeds;
  b4: b4_p1_blue_crystals, b4_p2_white_spikes, b4_p3_crystal_shards, b4_p4_geode, b4_p5_crystal_rock, b4_p6_gem_cluster;
  b5: b5_p1_crimson_veins, b5_p2_flesh_pod, b5_p3_bone_arch, b5_p4_bone_pile, b5_p5_tendril, b5_p6_red_nodule.
- Портреты стражей 768²: `AiArt/Guardians/guardian_sporonosa`, `guardian_gornilo`, `guardian_hor_bezdny`, `guardian_granilshchik`, `guardian_pulsar`.
- Иконки 256² RGBA: `AiArt/Icons/icon_puppet`, `icon_puppeteer`, `icon_legacy`, `icon_auto`.
- Новые модели (`Monsters/<имя>`, как остальные в ThirdPartyArt): Bat, Frog, Piranha, Rat, Shark, Slime, Snake, Spider, Wasp (с анимациями),
  LavaGolem, Octopus, Heart (статичные, без анимаций). Уже были: Alien, Bee, Cactus, Crab, Cthulhu, Cyclops, Demon, Ghost, GreenDemon,
  Mushroom, Skull, Q_Bat, Q_Dragon, Q_Skeleton, Q_Slime.

## Что сделать
1. **Пол**: в WorldView наложить текстуру пола биома (плитка, притушенно) так, чтобы стены/регионы читались как раньше; производительность —
   одна сетка/спрайт с тайлингом, без тысяч объектов.
2. **Декор**: детерминированная (по сиду карты) расстановка пропсов биома по полу, не на проходах шириной < 2 и не ближе 1.5 к выходу/алтарям;
   плотность ~1 на 12–20 м², 3 яруса размера, случайный поворот/отражение, лёгкое «дыхание» масштаба у светящихся (moss, algae, coral,
   crystals, veins), часть пропсов светится сквозь тьму (над DarknessMask, как мерцание). Чисто визуально, без коллайдеров. Domain:
   `DecorPlan` в WorldGenerator (позиции, индекс пропса, масштаб, поворот) + тесты на детерминизм и запретные зоны.
3. **Разнообразие биомов** (Domain + Unity, с тестами):
   - Г1: «грибные поляны» — регионы с множеством отбойников-грибов (они уже есть) и спорами;
   - Г2: лавовые реки — вытянутые зоны урона (огибают проход, не перекрывают его полностью), рядом угли;
   - Г3: течения — полосы, сносящие героя и существ, кувшинки-островки;
   - Г4: кристальные лабиринты — коридоры из колонн-кристаллов, рикошет даёт бонус скорости;
   - Г5: пульсирующие жилы — зоны, которые раз в 4–6 с «бьют» (телеграф 0.8 с).
   Форма карт по глубинам тоже разная (Г1 — пещеры, Г3 — острова среди воды, Г4 — узкие лабиринты, Г5 — кольцо вокруг логова).
4. **Модели**: стражам — свои тела: Спороносица → Mushroom, Горнило → LavaGolem (оранжевый тинт + свечение), Хор Бездны → Octopus,
   Гранильщик → Cyclops (кристальный тинт), Пульсар → Heart (багровый, пульс масштаба). Видам — подходящие модели: mite → Spider,
   glowworm → Wasp, leech → Snake, soul_slime → Slime, wisp → Bat. Новые виды (с записью в энциклопедии и lore, без портрета — запасной):
   «Пещерная крыса» (Rat, Г1), «Пиранья» (Piranha, Г3, стая), «Жаба-бомба» (Frog, Г3), «Глубинная акула» (Shark, Г3 элита). Баланс — по BALANCE_v14.
5. **Стражи в UI**: портрет стража в баннере пробуждения и в энциклопедии (запись стража открывается при победе).
6. **Иконки**: «Авто»/«Марионетка»/«Кукловод»/«Наследие» — на кнопках умений, узлах эволюции и экране наследия.
7. **Визуальный аудит**: пройди RunUi/RunEvolutionUi/RunCodexUi/RunSettings на перекрытия и обрезания на 2400×1080 (safe area) —
   известные: кнопки «Страж N» прижаты к верху окна настроек (сдвинь ниже, чтобы не касались рамки). Исправь найденное, список — в отчёт.

## Сдача
`git bundle create download/pm-v15.bundle zai/v14..zai/v15`, sha256 в `download/SHA256SUMS_v15.txt`; отчёт ≤ 15 строк: файлы, что сделано,
`dotnet run` PASSED N/N, что не сделано.
