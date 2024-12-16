﻿using LevelGeneration;
using UnityEngine;

namespace Hikaria.ItemMarker.Handlers.Markers
{
    public class LG_DisinfectionStationMarker : ItemMarkerBase
    {
        public override void SetupNavMarker(Component comp)
        {
            m_station = comp.Cast<LG_DisinfectionStation>();
            m_marker = GuiManager.NavMarkerLayer.PrepareGenericMarker(comp.gameObject);
            m_markerColor = new Color(0.467f, 0.098f, 1f);
            m_markerVisibleUpdateMode = ItemMarkerVisibleUpdateModeType.Manual;
            m_markerStyle = eNavMarkerStyle.PlayerPingDisinfection;
            m_terminalItem = m_station.m_terminalItem.Cast<LG_GenericTerminalItem>();
            m_markerTitle = m_terminalItem.TerminalItemKey;

            base.SetupNavMarker(comp);
        }

        private LG_DisinfectionStation m_station;
    }
}
