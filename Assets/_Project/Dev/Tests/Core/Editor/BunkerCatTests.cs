#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BunkerCatTests
{
    static T Read<T>(Object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Write(Object o, string field, object value) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    static IEnumerator WaitForGameTime(float seconds)
    {
        // This EditMode test enters Play Mode, but its iterator is still driven by
        // EditModeRunner, which does not wait for runtime WaitForSeconds instructions.
        float until = Time.time + seconds;
        float timeout = Time.realtimeSinceStartup + seconds + 3f;
        while (Time.time < until && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(until), "Game time must advance during the smoke check.");
    }

    [UnityTest]
    public IEnumerator ShortProductionWanderAndPet()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        float startupDeadline = Time.realtimeSinceStartup + 8f;
        while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" ||
            SceneTransitionOverlay.IsTransitioning || Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is not { IsReady: true }) &&
            Time.realtimeSinceStartup < startupDeadline) yield return null;
        Assert.That(LocalizationService.Instance, Is.Not.Null, "Use the production StartScreen bootstrap.");
        yield return null;
        var cat = Object.FindFirstObjectByType<CatWanderController>();
        Assert.That(cat, Is.Not.Null);
        Assert.That(cat.CompareTag("Player"), Is.False);
        Assert.That(cat.GetComponent<PlayerInteractor>(), Is.Null);
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Idle));
        var animator = cat.GetComponentInChildren<Animator>();
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CatIdle1"), Is.True);
        var body = cat.GetComponent<Rigidbody2D>();
        Vector2 origin = body.position;
        var player = PlayerRuntimeReference.CachedPlayer;
        Assert.That(player, Is.Not.Null);
        player.GetComponent<Rigidbody2D>().position = origin + Vector2.left * 4f;
        Physics2D.SyncTransforms();
        Write(cat, "stateUntil", Time.time);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (cat.State != CatWanderController.CatState.Walking && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Walking), "Cat must find a free destination on production floor.");
        Vector2 firstDestination = Read<Vector2>(cat, "destination");
        string trace = "origin=" + origin + " destination=" + firstDestination;
        float observeUntil = Time.time + .2f;
        while (Time.time < observeUntil)
        {
            yield return null;
            trace += "\n" + cat.State + " position=" + body.position;
        }
        Assert.That(Vector2.Distance(body.position, origin), Is.GreaterThan(.05f), trace);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CatWalk1"), Is.True);
        deadline = Time.realtimeSinceStartup + 3f;
        while (cat.State == CatWanderController.CatState.Walking && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Idle), "Walk returns to an idle pause.");
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CatIdle1"), Is.True);
        Write(cat, "stateUntil", Time.time);
        deadline = Time.realtimeSinceStartup + 3f;
        while (cat.State != CatWanderController.CatState.Walking && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Walking));

        // Move the real controlled player near the cat and use the shared selection and prompt.
        var playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        playerBody.position = body.position + Vector2.down * 1.3f;
        playerBody.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        var pet = cat.GetComponent<CatPetInteractable>();
        var interactor = player.GetComponent<PlayerInteractor>();
        deadline = Time.realtimeSinceStartup + 1f;
        while (interactor.GetCurrentInteractable() != pet && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(interactor.GetCurrentInteractable(), Is.SameAs(pet), "Pet available=" + pet.CanInteract +
            " interactor enabled=" + interactor.isActiveAndEnabled + " player=" + playerBody.position + " cat=" + body.position);
        var prompt = Object.FindFirstObjectByType<InteractionPromptUI>();
        Assert.That(Read<GameObject>(prompt, "promptPanel").activeSelf, Is.True);
        Assert.That(Read<TextMeshProUGUI>(prompt, "promptText").text, Is.EqualTo("[E] " + LocalizationService.Instance.Get("bunker.cat.pet")));
        interactor.GetCurrentInteractable().Interact();
        Vector2 petPosition = body.position;
        for (int i = 0; i < 20; i++) pet.Interact();
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Interacting));
        Assert.That(pet.CanInteract, Is.False);
        Assert.That(Object.FindObjectsByType<CatHeartFx>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CatIdle1"), Is.True);
        var heart = Object.FindFirstObjectByType<CatHeartFx>();
        Vector3 heartOrigin = heart.transform.position;
        float alpha = heart.GetComponent<SpriteRenderer>().color.a;
        yield return WaitForGameTime(.4f);
        Assert.That(Vector2.Distance(body.position, petPosition), Is.LessThan(.001f));
        Assert.That(heart.transform.position.y, Is.GreaterThan(heartOrigin.y));
        Assert.That(heart.GetComponent<SpriteRenderer>().color.a, Is.LessThan(alpha));
        yield return WaitForGameTime(1.5f);
        Assert.That(Object.FindFirstObjectByType<CatHeartFx>(), Is.Null);
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Idle));
        Assert.That(pet.CanInteract, Is.True);

        // Force a blocked route: a newly closed obstacle must cancel it, never tunnel through.
        var wall = new GameObject("Cat smoke obstacle", typeof(BoxCollider2D));
        var wallCollider = wall.GetComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(.2f, 3f);
        wall.transform.position = body.position + Vector2.right * (cat.GetComponent<CircleCollider2D>().bounds.extents.x + .15f);
        Physics2D.SyncTransforms();
        var route = typeof(CatWanderController).GetMethod("RouteClear", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That((bool)route.Invoke(cat, new object[] { Vector2.right, 2f }), Is.False);
        Write(cat, "destination", body.position + Vector2.right * 2f);
        typeof(CatWanderController).GetProperty("State").SetValue(cat, CatWanderController.CatState.Walking);
        Vector2 beforeWall = body.position;
        yield return WaitForGameTime(.25f);
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Idle));
        Assert.That(body.position.x - beforeWall.x, Is.LessThan(.15f));
        Object.Destroy(wall);
        yield return null;
        Write(cat, "stateUntil", Time.time);
        deadline = Time.realtimeSinceStartup + 3f;
        while (cat.State != CatWanderController.CatState.Walking && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(cat.State, Is.EqualTo(CatWanderController.CatState.Walking), "Cat resumes wandering after petting/obstacle.");
    }
}
#endif
