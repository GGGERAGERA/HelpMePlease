using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Material baseMaterial;

    private Material instanceMat;
    private Image img;

    public float normalSize = 0f;
    public float hoverSize = 0.03f;


    public Image targetImage;          // Картинка, которую будем менять
    public Sprite hoverSprite;         // Спрайт при наведении
    public Sprite normalSprite;        // Обычный спрайт

    void Start()
    {
        img = GetComponent<Image>();

        // создаём личную копию материала
        instanceMat = new Material(baseMaterial);

        img.material = instanceMat;

        instanceMat.SetFloat("_OutlineSize", normalSize);

        if (normalSprite == null && targetImage != null)
            normalSprite = targetImage.sprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        instanceMat.SetFloat("_OutlineSize", hoverSize);

        if (targetImage != null && hoverSprite != null)
            targetImage.sprite = hoverSprite;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        instanceMat.SetFloat("_OutlineSize", normalSize);

        if (targetImage != null && normalSprite != null)
            targetImage.sprite = normalSprite;
    }
}