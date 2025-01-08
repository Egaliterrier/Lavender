﻿using BehaviorDesigner.Runtime.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static Money;

namespace Lavender.FurnitureLib
{
    public static class FurniturePatches
    {
        [HarmonyPatch(typeof(SavableScriptableObject), nameof(SavableScriptableObject.LoadFromPath))]
        [HarmonyPrefix]
        static bool SavableScriptableObject_LoadFromPath_Prefix(SavableScriptableObject __instance, ref ScriptableObject __result)
        {
            if (!string.IsNullOrEmpty(__instance.addressableAssetPath))
            {
                if(__instance.addressableAssetPath.StartsWith("Lavender"))
                {
                    string sep = "<#>";
                    string path = __instance.addressableAssetPath.Substring(__instance.addressableAssetPath.IndexOf(sep) + 3);

                    try
                    {
                        Furniture f = FurnitureCreator.Create(path);

                        if (f != null)
                        {
                            if (Lavender.furnitureHandlers.TryGetValue(f.title, out Lavender.FurnitureHandler handler))
                            {
                                f = handler.Invoke(f);
                            }

                            __result = f;
                        }
                    }
                    catch (Exception e)
                    {
                        LavenderLog.Error(e.ToString());
                        __result = null;
                    }

                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(FurnitureShop), nameof(FurnitureShop.AddItem))]
        [HarmonyPrefix]
        static bool FurnitureShop_AddFurniture_Prefix(FurnitureShop __instance,ref bool __result, Furniture item, object meta, int amount)
        {
            BuildingSystem.FurnitureInfo furnitureInfo = __instance.availableFurnitures.Find((BuildingSystem.FurnitureInfo f) => f.furniture.title == item.title);
            if (furnitureInfo == null || furnitureInfo.furniture == null)
            {
                TaskItem taskItem = (TaskItem)ScriptableObject.CreateInstance(typeof(TaskItem));
                taskItem.itemName = item.title;
                taskItem.itemDetails = item.details;
                taskItem.image = item.image;
                taskItem.itemType = TaskItem.Type.Furnitures;
                __instance.availableFurnitures.Add(new BuildingSystem.FurnitureInfo(item, new BuildingSystem.FurnitureInfo.Meta(), taskItem, null, amount, null));
                __result = true;
            }
            furnitureInfo.amount += amount;
            __result = true;

            if (!BepinexPlugin.Settings.FurnitureShop_AddFurniture_Prefix_SkipOriginal.Value) return true;
            return false;
        }

        [HarmonyPatch(typeof(FurnitureShop), nameof(FurnitureShop.Restock))]
        [HarmonyPrefix]
        static bool BaseShop_Restock_Prefix(FurnitureShop __instance)
        {
            __instance.MoneyRestock();

            MethodInfo methodInfo = typeof(FurnitureShop).GetMethod("UpdateShopItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            methodInfo.Invoke(__instance, new object[] { });

            LavenderLog.Log($"Restocking '{(__instance.title != "" ? __instance.title : "One Stop Shop")}'");

            FurnitureShopName name = (__instance.title == "" ? FurnitureShopName.OneStopShop : (__instance.title == "Möbelmann Furnitures" ? FurnitureShopName.MoebelmannFurnitures : (__instance.title == "Jonasson's Shop" ? FurnitureShopName.SamuelJonasson : FurnitureShopName.None)));

            if (name != FurnitureShopName.None)
            {
                foreach (var pair in Lavender.furnitureShopRestockHandlers)
                {
                    __instance.availableFurnitures.AddRange(pair.Value.Invoke(name));
                }
            }

            if (!BepinexPlugin.Settings.FurnitureShop_Restock_Prefix_SkipOriginal.Value) return true;
            return false;
        }

        [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.AddFurniture))]
        [HarmonyPrefix]
        static bool BuildingSystem_AddFurniture_Prefix(BuildingSystem __instance, ref bool __result, Furniture furniture, GameObject gameObject, out GameObject savedGameObject, int amount = 1)
        {
            BuildingSystem.FurnitureInfo furnitureInfo = __instance.availableFurnitures.Find((BuildingSystem.FurnitureInfo f) => f.furniture.title == furniture.title && f.gameObject == null);
            if (gameObject != null)
            {
                gameObject.transform.SetParent((__instance.inventoryLocation != null) ? __instance.inventoryLocation : __instance.gameObject.transform);
                gameObject.transform.localPosition = Vector3.zero;
                if (!__instance.HasSaveableContent(gameObject))
                {
                    UnityEngine.Object.Destroy(gameObject);
                    gameObject = null;
                }
            }
            savedGameObject = gameObject;
            BuildingSystem.FurnitureInfo info = __instance.availableFurnitures.Find((BuildingSystem.FurnitureInfo f) => f.furniture.title == furniture.title);
            TaskItem taskItem = __instance.AddTaskItem(furniture, new BuildingSystem.FurnitureInfo.Meta(), info, amount);
            if (furnitureInfo == null || furnitureInfo.furniture == null || gameObject != null)
            {
                __instance.availableFurnitures.Add(new BuildingSystem.FurnitureInfo(furniture, new BuildingSystem.FurnitureInfo.Meta(), taskItem, gameObject, amount, null));
                __instance.availableFurnitures.Sort((BuildingSystem.FurnitureInfo slot1, BuildingSystem.FurnitureInfo slot2) => slot1.furniture.name.CompareTo(slot2.furniture.name));
                __result = true;

                return !BepinexPlugin.Settings.BuildingSystem_AddFurniture_Prefix_SkipOriginal.Value;
            }
            furnitureInfo.amount += amount;
            __result = true;

            return !BepinexPlugin.Settings.BuildingSystem_AddFurniture_Prefix_SkipOriginal.Value;
        }
    }
}
