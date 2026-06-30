using UnityEngine;
using UnityEngine.UI;

public class BunkerPlacementSystem : MonoBehaviour
{
    public static BunkerPlacementSystem Instance;

    [Header("Настройки")]
    [Tooltip("Слои, с которыми нельзя пересекаться при размещении")]
    [SerializeField] private LayerMask placementMask;

    private InteractableBunkerObject currentObject;
    private bool isMovingExisting = false;
    private bool isPlacingNewItem = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private CanvasGroup shopUIReference;

    private Color validColor;
    private Color invalidColor;

    private Camera mainCam;

    public bool IsPlacing => currentObject != null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (!IsPlacing) return;

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        currentObject.transform.position = mouseWorldPos;

        bool isValid = CheckPlacementValidity();
        currentObject.SetPlacementColor(isValid ? validColor : invalidColor);

        if (Input.GetMouseButtonDown(0) && isValid)
        {
            ConfirmPlacement();
        }
    }

    public void StartMovingExistingObject(InteractableBunkerObject obj, Color valid, Color invalid)
    {
        currentObject = obj;
        isMovingExisting = true;
        isPlacingNewItem = false;
        originalPosition = obj.transform.position;
        originalRotation = obj.transform.rotation;
        validColor = valid;
        invalidColor = invalid;
    }

    public void StartPlacingNewItem(GameObject prefab, int cost, CanvasGroup shopUI, Color valid, Color invalid)
    {
        if (!CurrencyManager.Instance.SpendGold(cost))
        {
            Debug.Log("Недостаточно средств!");
            return;
        }

        GameObject newObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        currentObject = newObj.GetComponent<InteractableBunkerObject>();

        isMovingExisting = false;
        isPlacingNewItem = true;
        validColor = valid;
        invalidColor = invalid;
        shopUIReference = shopUI;

        if (shopUI != null)
        {
            BunkerUIManager.Instance.OpenUI(shopUI);
        }
    }

    public void CancelPlacement()
    {
        if (currentObject == null) return;

        bool wasNewItem = isPlacingNewItem;

        if (isMovingExisting)
        {
            currentObject.transform.position = originalPosition;
            currentObject.transform.rotation = originalRotation;
        }
        else if (isPlacingNewItem)
        {
            CurrencyManager.Instance.AddGold(currentObject.GetBuyCost());
            Destroy(currentObject.gameObject);
        }

        currentObject.ResetColors();
        currentObject = null;

        if (wasNewItem && shopUIReference != null)
        {
            BunkerUIManager.Instance.OpenUI(shopUIReference);
        }
    }

    private void ConfirmPlacement()
    {
        currentObject.ResetColors();
        // BunkerSaveSystem.Instance.SaveObjectPosition(currentObject);
        currentObject = null;
    }

    private bool CheckPlacementValidity()
    {
        Collider2D objCollider = currentObject.GetCollider();
        if (objCollider == null) return true;

        Collider2D[] hits = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(placementMask);
        filter.useLayerMask = true;

        int count = objCollider.Overlap(filter, hits);

        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform != currentObject.transform)
            {
                return false;
            }
        }

        return true;
    }
}