using UnityEngine;

public class LongGrassObject : CompositeObject
{
    public bool IsCut { get; private set; }

    [ContextMenu("Update Collider")]
    public void UpdateCollider()
    {
        // Fit box collider size and position to sprite considering sprite pivot
        boxCollider.size = spriteRenderer.sprite.bounds.size;
        boxCollider.offset = spriteRenderer.sprite.bounds.center;
    }

    protected override void Awake()
    {
        base.Awake();

        // Add parts
        partInteractable = AddPart<PartInteractable>();
        partHighlightable = AddPart<PartHighlightable>();
        partIndicatable = AddPart<PartIndicatable>();

        // Initialize interaction
        interactionCut = new InteractionToolClick("Cut", "cut", ToolType.Cutter, OnCut);
        partInteractable.AddInteraction(interactionCut);
    }

    protected void Start()
    {
        // Initialize indicator
        partIndicatable.SetIcon(PartIndicatable.IconType.Resource);
        partIndicatable.SetOffsetDir(Transform.up);
    }

    [Header("References")]
    [SerializeField] private GameObject grassIngredientPfb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private Sprite cutSprite;

    private PartInteractable partInteractable;
    private PartIndicatable partIndicatable;
    private PartHighlightable partHighlightable;
    private InteractionToolClick interactionCut;

    private void OnCut()
    {
        // Change sprite
        spriteRenderer.sprite = cutSprite;

        // Spawn ingredient at 90 degrees offset from this
        Instantiate(grassIngredientPfb, Transform.position, Transform.rotation * Quaternion.Euler(0, 0, 90));

        // Disable highlight
        partHighlightable.SetCanHighlight(false);

        // Update size of collider
        UpdateCollider();

        // Set variables
        IsCut = true;
    }
}
