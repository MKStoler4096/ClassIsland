﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using Windows.Win32.UI.Accessibility;
using Microsoft.Extensions.Logging;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Enums;
using ClassIsland.Core.Models.Ruleset;
using static JetBrains.FormatRipper.FileExplorer.FileTypeExplorer;
using System.Text.Json;

namespace ClassIsland.Services;

public class RulesetService : IRulesetService
{
    public ILogger<RulesetService> Logger { get; }


    public RulesetService(ILogger<RulesetService> logger)
    {
        Logger = logger;

        eventProc = PfnWinEventProc;
        SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null, eventProc,
            0, 0,
            WINEVENT_OUTOFCONTEXT);
        ForegroundWindowChanged += (_, _) => NotifyStatusChanged();
    }

    private static WINEVENTPROC eventProc;
    private void PfnWinEventProc(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int child, uint thread, uint time)
    {
        Logger.LogTrace("Window event: {} HWND:{} {}", @event, hwnd, hook.Value);
        _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
        {
            ForegroundWindowChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public event EventHandler? ForegroundWindowChanged;

    public event EventHandler? StatusUpdated;
    public bool IsRulesetSatisfied(Ruleset ruleset)
    {
        var rulesetSatisfied = ruleset.Mode == RulesetLogicalMode.And;
        foreach (var i in ruleset.Rules)
        {
            if (!IRulesetService.Rules.TryGetValue(i.Id, out var rule))
            {
                Logger.LogWarning("找不到规则 {} 的注册信息。", i.Id);
                continue;
            }

            bool result;
            object? settings = null;
            var settingsType = rule.SettingsType;
            if (settingsType != null)
            {
                settings = i.Settings ?? Activator.CreateInstance(settingsType);
                if (settings is JsonElement json)
                {
                    settings = json.Deserialize(settingsType);
                }
            }
            if (rule.Handle != null)
            {
                result = rule.Handle(settings);
            }
            else
            {
                result = false;
                Logger.LogWarning("规则 {} 的处理程序没有注册，已默认其结果为 false.", rule.Id);
            }
            

            result ^= i.IsReversed;
            if (!result && ruleset.Mode == RulesetLogicalMode.And)
            {
                rulesetSatisfied = false;
                break;
            }
            if (result && ruleset.Mode == RulesetLogicalMode.Or)
            {
                rulesetSatisfied = true;
                break;
            }
        }
        rulesetSatisfied ^= ruleset.IsReversed;
        return rulesetSatisfied;
    }

    public void RegisterRuleHandler(string id, RuleRegistryInfo.HandleDelegate handler)
    {
        if (!IRulesetService.Rules.TryGetValue(id, out var ruleRegistryInfo))
        {
            throw new KeyNotFoundException($"找不到规则 {id}。");
        }

        ruleRegistryInfo.Handle += handler;
    }

    public void NotifyStatusChanged()
    {
        StatusUpdated?.Invoke(this, EventArgs.Empty);
    }
}