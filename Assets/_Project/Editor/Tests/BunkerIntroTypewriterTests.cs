using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class BunkerIntroTypewriterTests
{
    [Test]
    public void ProgressKeepsFullTmpContentAndOnlyChangesVisibilityLimit()
    {
        GameObject root = new("Intro View Test", typeof(RectTransform));
        BunkerIntroView view = root.AddComponent<BunkerIntroView>();
        TextMeshProUGUI main = CreateText("Main", root.transform);
        TextMeshProUGUI secondary = CreateText("Secondary", root.transform);

        try
        {
            SetField(view, "mainText", main);
            SetField(view, "systemText", secondary);

            view.SetText(
                "ABCD",
                "XY",
                BunkerIntroTextStyle.System,
                1,
                0,
                1f);

            Assert.That(main.text, Is.EqualTo("ABCD"));
            Assert.That(secondary.text, Is.EqualTo("XY"));
            Assert.That(main.maxVisibleCharacters, Is.EqualTo(1));
            Assert.That(secondary.maxVisibleCharacters, Is.Zero);

            view.SetText(
                "ABCD",
                "XY",
                BunkerIntroTextStyle.System,
                3,
                1,
                1f);

            Assert.That(main.text, Is.EqualTo("ABCD"));
            Assert.That(secondary.text, Is.EqualTo("XY"));
            Assert.That(main.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(secondary.maxVisibleCharacters, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent)
    {
        GameObject textObject = new(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        return textObject.AddComponent<TextMeshProUGUI>();
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {name}");
        field.SetValue(target, value);
    }
}
