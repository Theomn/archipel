using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

public class PlayerItem : SingletonMonoBehaviour<PlayerItem>
{
    [SerializeField] private LayerMask dropLayerMask;
    [SerializeField] private Transform hands;
    [SerializeField] private float handsLenght;
    [SerializeField] public Transform mouth;

    private struct DropData
    {
        public Receptacle receptacle;
        public Vector3 target;
    }

    public Vector3 initialHandsPosition { get; private set; }
    private bool isHoldingItem = false;
    private Item heldItem;
    private PlayerController controller;
    private HUDController hud;
    private bool isPaused;
    private bool unpauseFlag;



    protected override void Awake()
    {
        base.Awake();
        initialHandsPosition = hands.localPosition;
        //highlightParticles.Stop();
    }

    void Start()
    {
        controller = PlayerController.instance;
        hud = HUDController.instance;
    }

    void Update()
    {

        hands.localPosition = initialHandsPosition + SnapHandPosition();

        if (isPaused)
        {
            if (unpauseFlag)
            {
                isPaused = false;
                unpauseFlag = false;
            }
            return;
        }

        UpdateUI();

        if (isHoldingItem)
        {
            if (Input.GetButtonDown("Use"))
            {
                heldItem.Use();
            }
            else if (Input.GetButtonDown("Grab"))
            {
                if (CanDropItem(out var data))
                {
                    DropItem(data);
                }
                else
                {
                    heldItem.NegativeFeedback();
                }
            }
        }

        else if (!isHoldingItem)
        {
            var interactible = FindClosestInteractible(out var pos);
            if (Input.GetButtonDown("Grab") && interactible is Grabbable)
            {
                TakeItem((interactible as Grabbable).Grab());
            }
            if (Input.GetButtonDown("Use") && interactible is Useable)
            {
                (interactible as Useable).Use();
            }
        }
    }

    private void UpdateUI()
    {
        if (isHoldingItem)
        {
            if (heldItem.isUseable)
            {
                hud.use.Show(true, "Utiliser");
            }
            else
            {
                hud.use.Show(false);
            }
            if (CanDropItem(out var data))
            {
                if (data.receptacle != null)
                {
                    hud.grab.Show(true, "Placer");
                    hud.ShowHighlightParticles(data.receptacle.transform.position);
                }
                else
                {
                    hud.grab.Show(true, "Lacher");
                    hud.HideHighlightParticles();
                }
            }
            else
            {
                hud.grab.Show(false, "Lacher");
                hud.HideHighlightParticles();
            }
            hud.sit.Show(false);
        }

        else if (!isHoldingItem)
        {
            var interactible = FindClosestInteractible(out var pos);

            if (interactible == null) hud.HideHighlightParticles();
            else hud.ShowHighlightParticles(pos);

            if (interactible is Grabbable)
            {
                if (interactible is Receptacle && (!(interactible as Receptacle).isHoldingItem || (interactible as Receptacle).isBlocked))
                {
                    hud.grab.Show(false);
                    hud.HideHighlightParticles();
                }
                else
                {
                    hud.grab.Show(true, "Prendre");
                }
            }
            else
            {
                hud.grab.Show(false);
            }
            if (interactible is Useable)
            {
                hud.use.Show(true, "Utiliser");
            }
            else
            {
                hud.use.Show(false);
            }
            hud.sit.Show(true, "Penser");
        }
    }

    private void TakeItem(Item item)
    {
        if (!item)
        {
            return;
        }
        item.Take(hands.localPosition.y);
        item.transform.parent = hands;
        item.transform.DOKill();
        item.transform.DOLocalMove(Vector3.zero, 0.2f).SetEase(Ease.OutSine);
        isHoldingItem = true;
        heldItem = item;

    }

    private Interactible FindClosestInteractible(out Vector3 position)
    {
        var colliders = Physics.OverlapSphere(transform.position + controller.forward * 0.9f, 1f, 1 << Layer.interactible);
        if (colliders.Length == 0)
        {
            position = Vector3.zero;
            return null;
        }
        var closest = colliders.OrderBy(c => distanceToPlayer(c.transform)).ElementAt(0);
        position = closest.transform.position;
        return closest.GetComponent<Interactible>();
    }

    private void DropItem(DropData data)
    {
        heldItem.Drop();
        heldItem.transform.DOKill();

        if (data.receptacle != null)
        {
            var target = data.receptacle.Place(heldItem);
            DropAnimation(target);
            heldItem.transform.parent = data.receptacle.transform;
        }
        else
        {
            DropAnimation(data.target);
            heldItem.transform.parent = null;
        }
        isHoldingItem = false;
        heldItem = null;
    }

    private bool CanDropItem(out DropData data)
    {
        data = new DropData();
        var colliders = Physics.OverlapSphere(transform.position + controller.forward * 0.5f + Vector3.up * 0.1f, 0.3f, dropLayerMask);
        foreach (var collider in colliders)
        {
            // Receptacle detected
            if ((data.receptacle = collider.GetComponent<Receptacle>()) != null && !data.receptacle.isBlocked && !data.receptacle.isHoldingItem)
            {
                return true;
            }
            // Cannot drop on any solid collider
            if (!collider.isTrigger)
            {
                return false;
            }
        }
        if (Physics.Raycast(transform.position + controller.forward * 0.5f + Vector3.up, Vector3.down, out var hit, 1.5f, 1 << Layer.ground))
        {
            data.target = hit.point;
            return true;
        }
        return false;
    }

    private void DropAnimation(Vector3 target)
    {
        heldItem.transform.DOMoveX(target.x, 0.3f).SetEase(Ease.Linear);
        heldItem.transform.DOMoveZ(target.z, 0.3f).SetEase(Ease.Linear);
        heldItem.transform.DOMoveY(target.y, 0.25f).SetEase(Ease.InBack);
    }

    public void RemoveItem()
    {
        isHoldingItem = false;
        heldItem = null;
    }

    public void Pause(bool pause)
    {
        if (pause)
        {
            isPaused = true;
            unpauseFlag = false;
            hud.HideHighlightParticles();
        }
        else
        {
            unpauseFlag = true;
        }
    }

    private float distanceToPlayer(Transform t)
    {
        return (t.position - transform.position).sqrMagnitude;
    }

    private Vector3 SnapHandPosition()
    {
        Vector3 snapPosition;
        if (Mathf.Abs(controller.forward.x) > Mathf.Abs(controller.forward.z))
        {
            snapPosition = new Vector3(Mathf.Sign(controller.forward.x), 0, 0);
        }
        else
        {
            snapPosition = new Vector3(0, 0, Mathf.Sign(controller.forward.z));
        }
        return snapPosition * handsLenght + Vector3.back * 0.01f; // little offset backwards to make sure it appears in front of player when sideways
    }
}
