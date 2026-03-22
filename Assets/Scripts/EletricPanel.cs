using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class EletricPanel : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;

    [SerializeField] private SpriteRenderer lightOnSprite;
    [SerializeField] private SpriteRenderer lightOffSprite;

    [SerializeField] private float interactionRadius = 2f;
    private bool isPowered = true;
    private bool isInteracting = false;

    void Start()
    {
        if (playerController == null)
        {
            Debug.LogError("PlayerController reference is missing on EletricPanel.");
            return;
        }
    }

    void Update()
    {

    }

    public void OnClick()
    {
        Debug.Log("Electric Panel clicked.");
        if (isInteracting) return;
        
        if (!isPowered)
        {
            Debug.Log("Power is already cut! Waiting for an NPC to fix it.");
            return;
        }

        float distance = Vector2.Distance(transform.position, playerController.transform.position);
        bool isCloseEnough = distance <= interactionRadius;

        if (isCloseEnough)
        {
            isInteracting = true;
            TogglePower();
            isInteracting = false;
        }
        else
        {
            Debug.Log("Player is too far to interact with the electric panel.");
        }
    }

    public void TogglePower()
    {

        isPowered = !isPowered;
        if (isPowered)
        {
            Debug.Log("Power Restored!");
            lightOnSprite.color = new Color32(71, 255, 0, 255);
            lightOffSprite.color = new Color32(80, 80, 80, 255);
        }
        else
        {
            Debug.Log("Power Cut!");
            lightOnSprite.color = new Color32(80, 80, 80, 255);
            lightOffSprite.color = new Color32(255, 0, 0, 255);
            CallNearestWorker();
        }
    }

    private void CallNearestWorker()
    {
        ControllerNPC[] allNPCs = FindObjectsByType<ControllerNPC>(FindObjectsSortMode.None);
        ControllerNPC closestNPC = null;
        float closestDistance = float.MaxValue;

        foreach (ControllerNPC npc in allNPCs)
        {
            if (npc.shapeData != null && (npc.shapeData.type == ShapeType.Hexagon || npc.shapeData.type == ShapeType.Square))
            {
                float distance = Vector2.Distance(transform.position, npc.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestNPC = npc;
                }
            }
        }

        if (closestNPC != null)
        {
            Debug.Log("Nearest worker found! Sending " + closestNPC.gameObject.name + " to fix the panel.");
            closestNPC.GoDoTask(transform.position, () => StartCoroutine(FixPanelRoutine(closestNPC)));
        }
        else
        {
            Debug.LogWarning("No Hexagon or Square NPCs found to fix the electric panel!");
        }
    }

    private IEnumerator FixPanelRoutine(ControllerNPC worker)
    {
        Debug.Log(worker.gameObject.name + " is fixing the panel... (takes 3 seconds)");
        yield return new WaitForSeconds(3f);

        if (!isPowered)
        {
            TogglePower(); // Turn it back on
        }

        worker.ResumeNormalBehavior(); // Let the NPC go back to its job
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
