using UnityEngine;

public static class AICommentGenerator
{
    public static string GetComment(bool victory)
    {
        int kills = RunStatsManager.Instance != null ? RunStatsManager.Instance.Kills : 0;
        float time = RunStatsManager.Instance != null ? RunStatsManager.Instance.RunTime : 0f;
        int level = ExperienceManager.Instance != null ? ExperienceManager.Instance.currentLevel : 1;

        string weaponName = "";

        if (!victory && time < 60f)
            return RandomFrom(fastDeath);

        if (victory)
            return RandomFrom(bossKilled);

        if (!victory)
            return RandomFrom(death);

        if (kills >= 300)
            return RandomFrom(manyKills);

        if (level >= 10)
            return RandomFrom(manyLevels);

        if (weaponName.Contains("pistol") || weaponName.Contains("пистолет"))
            return RandomFrom(pistol);

        if (weaponName.Contains("laser") || weaponName.Contains("лазер"))
            return RandomFrom(laser);

        if (weaponName.Contains("rocket") || weaponName.Contains("ракет"))
            return RandomFrom(rocketLauncher);

        return RandomFrom(general);
    }

    private static string RandomFrom(string[] phrases)
    {
        if (phrases == null || phrases.Length == 0)
            return "";

        return phrases[Random.Range(0, phrases.Length)];
    }

    private static readonly string[] general =
    {
        "Анализ завершён. Субъект всё ещё жив. Причина уточняется.",
        "Эксперимент продолжается. К сожалению.",
        "Вероятность выживания субъекта превысила расчётную ошибку.",
        "Напоминаем: героизм не входит в список разрешённых действий.",
        "Результаты эксперимента признаны статистически подозрительными.",
        "Субъект снова нарушил рекомендации по скорейшей гибели.",
        "Наблюдение продолжается. Попытки выглядеть круто зафиксированы.",
        "Анализ поведения завершён. Паника оказалась эффективнее стратегии.",
        "Биологический объект проявляет необоснованную тягу к выживанию.",
        "Эксперимент вышел из-под контроля. Снова."
    };

    private static readonly string[] manyKills =
    {
        "Количество уничтоженных целей вызывает лёгкое беспокойство.",
        "Субъект ликвидировал слишком много экземпляров. Требуется пересчёт бюджета.",
        "Эффективность субъекта временно признана неприлично высокой.",
        "Вы успешно уменьшили популяцию поверхности. Она против.",
        "Производство зомби будет увеличено для сохранения научной честности."
    };

    private static readonly string[] death =
    {
        "Эксперимент завершён успешно. Субъект погиб согласно плану.",
        "Причина смерти: поверхность.",
        "Биологическая единица отключилась.",
        "Вы были ближе к успеху, чем большинство предыдущих образцов.",
        "Не расстраивайтесь. Предыдущий субъект продержался 12 секунд."
    };

    private static readonly string[] fastDeath =
    {
        "Эксперимент завершился раньше, чем успел стать интересным.",
        "Субъект продемонстрировал рекордную скорость прекращения существования.",
        "Поверхность победила быстрее обычного.",
        "Рекомендация: в следующий раз начать с дыхания.",
        "Отчёт сохранён. Содержит в основном слово «ой»."
    };

    private static readonly string[] bossKilled =
    {
        "Тестовый образец «БОСС» признан неудовлетворительным.",
        "Поздравляем. Вы сломали очередную дорогостоящую разработку.",
        "Уровень угрозы пересматривается.",
        "Замена босса уже заказана.",
        "Субъект продемонстрировал признаки компетентности. Это недопустимо."
    };

    private static readonly string[] pistol =
    {
        "Субъект демонстрирует тревожную веру в надёжность старых технологий.",
        "Пистолет выбран. Романтика прошлого века всё ещё опасна.",
        "Оружие примитивное. Результаты раздражающе приемлемые."
    };

    private static readonly string[] laser =
    {
        "Субъект предпочитает испарять проблемы вместо их решения.",
        "Использование лазера подтверждает отсутствие терпения.",
        "Лучевая технология применена без разрешения отдела безопасности. Отдел безопасности испарён."
    };

    private static readonly string[] rocketLauncher =
    {
        "Зафиксирована тяга субъекта решать проблемы взрывом.",
        "Анализ показал: если всё взрывается, субъекта это устраивает.",
        "Ракетница выбрана. Точность признана необязательной."
    };

    private static readonly string[] manyLevels =
    {
        "Субъект накопил подозрительное количество модификаций.",
        "Биологическая система субъекта больше не соответствует документации.",
        "Количество улучшений превысило рекомендуемые нормы здравого смысла."
    };
}
