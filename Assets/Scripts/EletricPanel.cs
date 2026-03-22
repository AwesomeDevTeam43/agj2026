using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class EletricPanel : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;

    [SerializeField] private SpriteRenderer lightOnSprite;
    [SerializeField] private SpriteRenderer lightOffSprite;

    [SerializeField] private float interactionRadius = 2f;
    private bool isPowered = true;
    private bool isInteracting = false;
    
    // Debug
    private ControllerNPC activeWorker = null;

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
        // Debug visibility: draws a line to the chosen worker while they are traveling
        if (activeWorker != null && !isPowered)
        {
            Debug.DrawLine(transform.position, activeWorker.transform.position, Color.red);
        }

        if (playerController != null)
        {
            float distance = Vector2.Distance(transform.position, playerController.transform.position);
            if (distance <= interactionRadius)
            {
                HandleTooltip();
            }
            else
            {
                TooltipManager.Instance.HideTooltip();
            }
        }
    }

    void HandleTooltip()
    {   if (isPowered)
        {
            TooltipManager.Instance.ShowTooltip("Click with LMB to Cut Power", transform.position);
        }
        else
        {
            TooltipManager.Instance.ShowTooltip("Power is cut! cannot interact", transform.position);
        }
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

        NavMeshPath path = new NavMeshPath();
        
        // Ensure we find a valid target position on the NavMesh
        Vector3 targetPos = transform.position;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
             targetPos = hit.position;
        }

        foreach (ControllerNPC npc in allNPCs)
        {
            if (npc.shapeData != null && (npc.shapeData.type == ShapeType.Hexagon || npc.shapeData.type == ShapeType.Square))
            {
                // Calculate path to find true walking distance
                if (NavMesh.CalculatePath(npc.transform.position, targetPos, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        float distance = 0f;
                        for (int i = 1; i < path.corners.Length; i++)
                        {
                            distance += Vector2.Distance(path.corners[i - 1], path.corners[i]);
                        }

                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestNPC = npc;
                        }
                    }
                }
            }
        }

        // Se o pathfinding falhar em tudo, tenta por pure straight-line distance como fallback
        if (closestNPC == null)
        {
            foreach (ControllerNPC npc in allNPCs)
            {
                if (npc.shapeData != null && (npc.shapeData.type == ShapeType.Hexagon || npc.shapeData.type == ShapeType.Square))
                {
                    float dist = Vector2.Distance(transform.position, npc.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestNPC = npc;
                    }
                }
            }
        }

        if (closestNPC != null)
        {
            activeWorker = closestNPC;
            Debug.Log("Nearest worker found! Sending " + closestNPC.gameObject.name + " to fix the panel.");
            closestNPC.GoDoTask(targetPos, () => StartCoroutine(FixPanelRoutine(closestNPC)));
        }
        else
        {
            activeWorker = null;
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

        activeWorker = null; // Clear the debug reference
        worker.ResumeNormalBehavior(); // Let the NPC go back to its job
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        if (activeWorker != null && !isPowered)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, activeWorker.transform.position);
        }
    }
}
