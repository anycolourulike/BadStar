using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Combat;

//Healing pad: put on an object with a trigger collider. Heals the real Health of the
//characters standing on it (the HUD bar follows Health on its own).
[RequireComponent(typeof(Collider))]
public class HealthRegenScript : MonoBehaviour
{
    [Tooltip("Shown while the pad is healing someone")]
    [SerializeField] GameObject healthRegenFX;

    [Header("Who")]
    [SerializeField] string[] healTags = { "Player", "Companion" };

    [Header("Healing")]
    [Tooltip("Health per second while standing on the pad. 0 = heal to full instantly on entering")]
    [SerializeField] float healPerSecond = 25f;
    [Tooltip("Also refill robot energy when a heal starts")]
    [SerializeField] bool refillEnergy = true;

    [Header("Limits")]
    [Tooltip("Seconds the pad is offline after it finishes healing someone to full. 0 = no cooldown")]
    [SerializeField] float cooldown = 0f;
    [Tooltip("How many full heals the pad gives before it switches off for good. 0 = unlimited")]
    [SerializeField] int maxUses = 0;
    [Tooltip("Shown while the pad is cooling down or used up (optional)")]
    [SerializeField] GameObject offlineFX;

    readonly HashSet<Health> inside = new HashSet<Health>();
    readonly HashSet<Health> energised = new HashSet<Health>();
    float cooldownLeft;
    int usesLeft;

    bool UsedUp => maxUses > 0 && usesLeft <= 0;
    bool Online => cooldownLeft <= 0f && !UsedUp;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        usesLeft = maxUses;
        SetFX(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health == null || !CanHeal(health)) return;
        inside.Add(health);
        if (healPerSecond <= 0f && Online) HealToFull(health);
    }

    void OnTriggerExit(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health == null) return;
        inside.Remove(health);
        energised.Remove(health);
    }

    void OnDisable()
    {
        inside.Clear();
        energised.Clear();
        CancelInvoke();
        SetFX(false);
    }

    void Update()
    {
        if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;
        inside.RemoveWhere(h => h == null || h.IsDead() || !h.isActiveAndEnabled);

        bool healing = false;
        if (Online && healPerSecond > 0f)
        {
            //Copy: a full heal can start the cooldown part-way through the loop
            foreach (var health in new List<Health>(inside))
            {
                if (!Online) break;
                if (health.GetFraction() >= 1f) continue;
                healing = true;
                if (refillEnergy && energised.Add(health)) RefillEnergy(health);
                health.Heal(healPerSecond * Time.deltaTime);
                if (health.GetFraction() >= 1f) UseCharge();
            }
        }
        SetFX(healing);
    }

    bool CanHeal(Health health)
    {
        if (health.IsDead()) return false;
        foreach (var t in healTags)
            if (health.CompareTag(t)) return true;
        return false;
    }

    void HealToFull(Health health)
    {
        if (health.GetFraction() >= 1f) return; //Already full: don't waste a charge
        health.Heal(health.GetMaxHealth());
        if (refillEnergy) RefillEnergy(health);
        UseCharge();
        if (healthRegenFX != null)
        {
            CancelInvoke(nameof(HideFX));
            healthRegenFX.SetActive(true);
            Invoke(nameof(HideFX), 1f); //Brief flash for an instant heal
        }
    }

    void RefillEnergy(Health health)
    {
        var vitals = health.GetComponent<PlayerVitals>();
        if (vitals != null) vitals.RefillEnergy();
    }

    void UseCharge()
    {
        if (maxUses > 0) usesLeft--;
        if (cooldown > 0f) cooldownLeft = cooldown;
        energised.Clear();
    }

    void HideFX()
    {
        if (healthRegenFX != null) healthRegenFX.SetActive(false);
    }

    void SetFX(bool healing)
    {
        //Leave the instant-heal flash alone until it times out
        if (healthRegenFX != null && !IsInvoking(nameof(HideFX)) && healthRegenFX.activeSelf != healing)
            healthRegenFX.SetActive(healing);
        if (offlineFX != null && offlineFX.activeSelf == Online)
            offlineFX.SetActive(!Online);
    }
}
