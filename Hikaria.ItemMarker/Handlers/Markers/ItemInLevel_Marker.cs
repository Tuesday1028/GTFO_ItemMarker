﻿using AIGraph;
using GameData;
using Hikaria.Core.Interfaces;
using Hikaria.ItemMarker.Managers;
using LevelGeneration;
using Player;
using SNetwork;
using TheArchive.Core.Models;
using TheArchive.Core.ModulesAPI;
using UnityEngine;

namespace Hikaria.ItemMarker.Handlers.Markers
{
    public class ItemInLevel_Marker : ItemMarkerBase, IOnGameStateChanged
    {
        public override void SetupNavMarker(Component comp)
        {
            m_item = comp.Cast<ItemInLevel>();
            var itemDataBlock = m_item.ItemDataBlock;
            if (!ValidItemSlots.Contains(itemDataBlock.inventorySlot))
            {
                Destroy(this);
                return;
            }
            m_item.internalSync.add_OnSyncStateChange((Action<ePickupItemStatus, pPickupPlacement, PlayerAgent, bool>)OnItemStateChange);
            m_navMarkerPlacer = m_item.GetComponent<PlaceNavMarkerOnGO>();
            m_itemSlot = itemDataBlock.inventorySlot;
            m_itemShowUses = !itemDataBlock.GUIShowAmmoInfinite && itemDataBlock.GUIShowAmmoTotalRel;
            switch (PlayerAmmoStorage.GetAmmoTypeFromSlot(itemDataBlock.inventorySlot))
            {
                case AmmoType.CurrentConsumable:
                    m_itemCostOfBullet = 1f;
                    if (itemDataBlock.ConsumableAmmoMax == m_itemCostOfBullet)
                        m_itemShowUses = false;
                    break;
                case AmmoType.ResourcePackRel:
                    m_itemCostOfBullet = 20f;
                    break;
                default:
                    m_itemShowUses = false;
                    break;
            }
            m_allowDiscoverScan = m_item.container == null && m_itemSlot != InventorySlot.InPocket && m_itemSlot != InventorySlot.Pickup;
            m_marker = GuiManager.NavMarkerLayer.PrepareGenericMarker(m_item.gameObject);
            m_markerStyle = GetComponentInChildren<iPlayerPingTarget>()?.PingTargetStyle ?? eNavMarkerStyle.LocationBeacon;
            m_markerTitle = itemDataBlock.publicName;
            m_terminalItem = m_item.GetComponentInChildren<LG_GenericTerminalItem>();
            if (ItemInLevelMarkerDescriptions.Value.TryGetValue(itemDataBlock.persistentID, out var desc))
            {
                m_markerColor = desc.Color;
                m_markerTitle = desc.Title;
                if (desc.UsePublicName)
                {
                    m_markerTitle = m_item.PublicName;
                }
                if (desc.UseTerminalItemKey)
                {
                    m_markerTitleUseTerminalItemKey = true;
                    if (m_terminalItem != null)
                        m_markerTitle = m_terminalItem.TerminalItemKey;
                }
                m_markerVisibleUpdateMode = desc.VisibleUpdateMode;
                m_markerVisibleWorldDistance = desc.VisibleWorldDistance;
                m_markerVisibleCourseNodeDistance = desc.VisibleCourseNodeDistance;
                m_markerAlpha = desc.Alpha;
                m_markerAlphaADS = desc.AlphaADS;
                m_markerAlwaysShowTitle = desc.AlwaysShowTitle;
                m_markerAlwaysShowDistance = desc.AlwaysShowDistance;
                m_markerPingFadeOutTime = desc.PingFadeOutTime;
                if (desc.UseCustomIcon)
                {
                    if (IconManager.TryGetCustomIcon(desc.CustomIconFileName, out var sprite))
                    {
                        var renderers = m_marker.m_iconHolder.GetComponentsInChildren<SpriteRenderer>(true);
                        foreach (var renderer in renderers)
                        {
                            renderer.sprite = sprite;
                        }
                    }
                }
            }

            ItemInLevelMarkerLookup[m_item.GetInstanceID()] = this;

            base.SetupNavMarker(comp);

            if (m_itemSlot == InventorySlot.InLevelCarry)
                m_marker.SetTitle($"{m_markerTitle} <size=75%><color=red>未拾起</color></size>");
        }


        protected override void Update()
        {
            if (!IsPlacedInLevel)
                return;

            base.Update();
        }

        protected override void OnDestroy()
        {
            ItemInLevelMarkerLookup.Remove(m_item.GetInstanceID());

            base.OnDestroy();
        }

        public void OnItemStateChange(ePickupItemStatus status, pPickupPlacement placement, PlayerAgent playerAgent, bool isRecall)
        {
            if (status == ePickupItemStatus.PlacedInLevel)
            {
                IsPlacedInLevel = true;
            }
            else if (status == ePickupItemStatus.PickedUp)
            {
                IsPlacedInLevel = false;
            }
        }

        public void OnItemCustomDataUpdate(pItemData_Custom custom)
        {
            if (m_itemSlot != InventorySlot.InLevelCarry)
                return;
            if (custom.byteState > 0) // HSU, CELL...
                AttemptInteract(eNavMarkerInteractionType.Hide);
        }

        public override void OnPlayerPing()
        {
            if (!IsPlacedInLevel)
                return;

            base.OnPlayerPing();
        }

        public override void OnTerminalPing()
        {
            if (!IsPlacedInLevel)
                return;

            base.OnTerminalPing();
        }

        public override void OnPlayerCourseNodeChanged(AIG_CourseNode newNode)
        {
            if (!IsPlacedInLevel)
                return;

            base.OnPlayerCourseNodeChanged(newNode);
        }

        public override void OnPlayerZoneChanged(LG_Zone newZone)
        {
            if (!IsPlacedInLevel)
                return;

            base.OnPlayerZoneChanged(newZone);
        }

        public override void OnPlayerDimensionChanged(Dimension newDim)
        {
            if (!IsPlacedInLevel)
                return;

            base.OnPlayerDimensionChanged(newDim);
        }

        public void OnGameStateChanged(eGameStateName preState, eGameStateName nextState)
        {
            if (nextState == eGameStateName.InLevel)
            {
                UpdateItemUsesLeft();
            }
        }

        private void UpdateItemUsesLeft()
        {
            if (m_itemShowUses)
            {
                m_itemUsesLeft = Mathf.FloorToInt(m_item.GetCustomData().ammo / m_itemCostOfBullet);
                m_marker.SetTitle($"{m_markerTitle} ×{m_itemUsesLeft}");
            }
        }

        protected override void OnDevUpdate()
        {
            if (!IsPlacedInLevel)
            {
                AttemptInteract(eNavMarkerInteractionType.Hide);
                return;
            }

            if (LocalPlayerAgent == null || LocalPlayerAgent.CourseNode == null || CourseNode == null)
                return;

            if (m_itemSlot == InventorySlot.InLevelCarry)
            {
                m_navMarkerPlacer?.m_marker?.SetVisible(false);

                if (!m_item.internalSync.GetCurrentState().placement.hasBeenPickedUp)
                    m_marker.SetTitle($"<color=red>未拾起</color>\n{m_markerTitle}");
                else
                    m_marker.SetTitle(m_markerTitle);

                if (m_item.GetCustomData().byteState > 0) // HSU, CELL...
                {
                    AttemptInteract(eNavMarkerInteractionType.Hide);
                    return;
                }

                if (!m_item.Cast<CarryItemPickup_Core>().IsInteractable)
                {
                    AttemptInteract(eNavMarkerInteractionType.Hide);
                    return;
                }

                AttemptInteract(eNavMarkerInteractionType.Show);
                return;
            }

            if (m_itemSlot == InventorySlot.ResourcePack || m_itemSlot == InventorySlot.Consumable)
            {
                if (CourseNode.m_zone.ID == LocalPlayerAgent.CourseNode.m_zone.ID)
                {
                    AttemptInteract(eNavMarkerInteractionType.Show);
                    if (m_itemShowUses)
                        m_marker.SetTitle($"{m_terminalItem?.TerminalItemKey ?? m_item.PublicName} ×{m_itemUsesLeft}");
                    else
                        m_marker.SetTitle(m_terminalItem?.TerminalItemKey ?? m_item.PublicName);
                    return;
                }
                if (Vector3.Distance(m_item.transform.position, LocalPlayerAgent.transform.position) <= m_markerVisibleWorldDistance)
                {
                    AttemptInteract(eNavMarkerInteractionType.Show);
                    m_marker.SetTitle($"<color=red>不同区域</color>\n{m_terminalItem?.TerminalItemKey ?? m_item.PublicName}");
                    return;
                }
            }

            AttemptInteract(eNavMarkerInteractionType.Hide);
        }

        protected override void CaptureToBuffer(eBufferType bufferType)
        {
            base.CaptureToBuffer(bufferType);

            m_buffers[bufferType] = new()
            {
                IsPlacedInLevel = IsPlacedInLevel
            };
        }

        protected override void RecallBuffer(eBufferType bufferType)
        {
            base.RecallBuffer(bufferType);

            if (!m_buffers.TryGetValue(bufferType, out var state))
            {
                IsPlacedInLevel = m_item.internalSync.GetCurrentState().status == ePickupItemStatus.PlacedInLevel;
                return;
            }

            IsPlacedInLevel = state.IsPlacedInLevel;
        }

        protected override void ResetBuffer()
        {
            base.ResetBuffer();

            m_buffers.Clear();
        }

        private bool IsPlacedInLevel
        {
            get => m_isPlacedInLevel;
            set
            {
                m_isPlacedInLevel = value;
                m_navMarkerPlacer?.m_marker?.SetVisible(false);
                if (!value)
                {
                    m_markerForceVisibleTimer = 0f;
                    if (m_marker.m_pingRoutine != null)
                    {
                        CoroutineManager.StopCoroutine(m_marker.m_pingRoutine);
                        m_marker.Scale(m_marker.m_pingObj, m_marker.m_pinStartScale, m_marker.m_pinStartScale, Color.white, Color.white, 0f);
                    }
                    AttemptInteract(eNavMarkerInteractionType.Hide);
                }
                else
                {
                    ForceUpdate();
                }
            }
        }

        protected override void OnManualUpdate()
        {
            UpdateItemUsesLeft();
            if (m_itemSlot == InventorySlot.InLevelCarry)
            {
                m_navMarkerPlacer?.m_marker?.SetVisible(false);

                if (!m_item.internalSync.GetCurrentState().placement.hasBeenPickedUp)
                    m_marker.SetTitle($"{m_markerTitle}\n<color=red>未拾起</color>");
                else
                    m_marker.SetTitle(m_markerTitle);

                if (m_item.GetCustomData().byteState > 0) // HSU, CELL...
                {
                    AttemptInteract(eNavMarkerInteractionType.Hide);
                    return;
                }

                if (!m_item.Cast<CarryItemPickup_Core>().IsInteractable)
                {
                    AttemptInteract(eNavMarkerInteractionType.Hide);
                    return;
                }
            }

            if (!IsPlacedInLevel)
            {
                AttemptInteract(eNavMarkerInteractionType.Hide);
                return;
            }

            AttemptInteract(eNavMarkerInteractionType.Show);
        }

        public override AIG_CourseNode CourseNode => m_item.CourseNode;

        public override bool AllowDiscoverScan => base.AllowDiscoverScan && m_allowDiscoverScan;

        private ItemInLevel m_item;
        private float m_itemCostOfBullet = 1f;
        private bool m_itemShowUses;
        private int m_itemUsesLeft;
        private InventorySlot m_itemSlot;

        private PlaceNavMarkerOnGO m_navMarkerPlacer;
        private bool m_allowDiscoverScan;
        private bool m_isPlacedInLevel = true;

        private readonly Dictionary<eBufferType, pItemInLevelMarkerState> m_buffers = new();

        public struct pItemInLevelMarkerState
        {
            public bool IsPlacedInLevel;
        }

        private static CustomSettings<Dictionary<uint, ItemInLevelMarkerDescription>> ItemInLevelMarkerDescriptions = new("ItemInLevelMarkerDescriptions", new());

        private class ItemInLevelMarkerDescription
        {
            public uint ItemID { get; set; } = 0U;
            public string DataBlockName { get; set; } = string.Empty;
            public string PublicName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public bool UsePublicName { get; set; } = false;
            public bool UseTerminalItemKey { get; set; } = false;
            public SColor Color { get; set; } = UnityEngine.Color.white;
            public ItemMarkerVisibleUpdateModeType VisibleUpdateMode { get; set; } = ItemMarkerVisibleUpdateModeType.World;
            public float VisibleWorldDistance { get; set; } = 30f;
            public int VisibleCourseNodeDistance { get; set; } = 1;
            public float Alpha { get; set; } = 0.9f;
            public float AlphaADS { get; set; } = 0.4f;
            public float IconScale { get; set; } = 0.4f;
            public float PingFadeOutTime { get; set; } = 12f;
            public bool AlwaysShowTitle { get; set; } = false;
            public bool AlwaysShowDistance { get; set; } = false;
            public bool UseCustomIcon { get; set; } = false;
            public string CustomIconFileName { get; set; } = string.Empty;
        }

        internal static void OnGameDataInitialized()
        {
            foreach (var block in ItemDataBlock.GetAllBlocks())
            {
                if (!ValidItemSlots.Contains(block.inventorySlot))
                    continue;

                if (!ItemInLevelMarkerDescriptions.Value.TryGetValue(block.persistentID, out var desc))
                {
                    desc = new()
                    {
                        ItemID = block.persistentID,
                        DataBlockName = block.name,
                        PublicName = block.publicName,
                        Title = block.publicName,
                        VisibleUpdateMode = GetDefaultUpdateModeForSlot(block.inventorySlot),
                        AlwaysShowTitle = block.inventorySlot == InventorySlot.InLevelCarry,
                        AlwaysShowDistance = block.inventorySlot == InventorySlot.InLevelCarry,
                        UsePublicName = block.inventorySlot == InventorySlot.InLevelCarry,
                        UseTerminalItemKey = block.inventorySlot == InventorySlot.InPocket || block.inventorySlot == InventorySlot.Pickup,
                    };
                    ItemInLevelMarkerDescriptions.Value[block.persistentID] = desc;
                }
                else
                {
                    desc.DataBlockName = block.name;
                    desc.ItemID = block.persistentID;
                    desc.PublicName = block.publicName;
                }
            }
        }

        private static ItemMarkerVisibleUpdateModeType GetDefaultUpdateModeForSlot(InventorySlot slot)
        {
            switch (slot)
            {
                case InventorySlot.ResourcePack:
                case InventorySlot.Consumable:
                    return ItemMarkerVisibleUpdateModeType.World;
                case InventorySlot.InPocket:
                case InventorySlot.Pickup:
                    return ItemMarkerVisibleUpdateModeType.Zone;
                case InventorySlot.InLevelCarry:
                    return ItemMarkerVisibleUpdateModeType.Manual;
                default:
                    return ItemMarkerVisibleUpdateModeType.World;
            }
        }

        private static readonly InventorySlot[] ValidItemSlots = { InventorySlot.ResourcePack, InventorySlot.Consumable, InventorySlot.Pickup, InventorySlot.InPocket, InventorySlot.InLevelCarry };
        internal static readonly Dictionary<int, ItemInLevel_Marker> ItemInLevelMarkerLookup = new();
    }
}