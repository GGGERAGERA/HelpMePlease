using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CircularAnomalyPropsSmoke
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InsideOrbitsOutsideStaysAndEffectStops()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var data = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var zone = new GameObject("Circular anomaly").AddComponent<GravityZone>();
        zone.Initialize(data, null, new Vector2(12, 10));
        zone.ConfigureOrbit(1, 1, 1, 0, 0, 0);
        var profile = Resources.Load<PropScatterProfile>("PropScatterProfile");
        Assert.That(profile, Is.Not.Null);
        var props = new GameObject[4];
        var starts = new[] { new Vector3(2, 0, 0), new Vector3(0, -3, 0), new Vector3(9, 0, 0), new Vector3(-2, 0, 0) };
        var marker = typeof(GravityZone).Assembly.GetType("AnomalyMovableProp");
        for (int i = 0; i < props.Length; i++)
        {
            props[i] = Object.Instantiate(profile.entries[i].prefab, starts[i], Quaternion.identity);
            if (i < 3 && marker != null) props[i].AddComponent(marker);
        }
        Vector3 scale = props[0].transform.localScale;
        var sprite = props[0].GetComponentInChildren<SpriteRenderer>().sprite;
        Physics2D.SyncTransforms();
        // This EditMode runner enters real Play Mode; drive elapsed game time explicitly.
        float until = Time.time + .6f;
        while (Time.time < until) yield return null;
        for (int i = 0; i < 2; i++)
        {
            Assert.That(Vector3.Distance(props[i].transform.position, starts[i]), Is.GreaterThan(.05f), "Inside prop must orbit");
            Assert.That(props[i].transform.position.magnitude, Is.EqualTo(starts[i].magnitude).Within(.002f));
        }
        Assert.That(props[2].transform.position, Is.EqualTo(starts[2]), "Outside stays still");
        Assert.That(props[3].transform.position, Is.EqualTo(starts[3]), "No marker stays still");
        Assert.That(props[0].transform.localScale, Is.EqualTo(scale));
        Assert.That(props[0].transform.rotation, Is.EqualTo(Quaternion.identity));
        Assert.That(props[0].GetComponentInChildren<SpriteRenderer>().sprite, Is.SameAs(sprite));
        Assert.That(props[0].GetComponentInChildren<Rigidbody2D>(), Is.Null);
        props[0].transform.position = starts[2];
        until = Time.time + .15f;
        while (Time.time < until) yield return null;
        Assert.That(props[0].transform.position, Is.EqualTo(starts[2]), "Leaving zone stops motion");
        Vector3 stopped = props[1].transform.position;
        zone.Despawn();
        until = Time.time + .15f;
        while (Time.time < until) yield return null;
        Assert.That(props[1].transform.position, Is.EqualTo(stopped), "Despawn stops motion");
        Object.Destroy(data);
        Directory.CreateDirectory("Artifacts/GeneratedQA/CircularAnomalyProps");
        File.WriteAllText("Artifacts/GeneratedQA/CircularAnomalyProps/checks.txt",
            "PASS: existing props inside orbit at constant radius; outside and unmarked stay still; scale, rotation, sprite unchanged; no Rigidbody2D; exit and despawn stop motion.\n");
    }
}
