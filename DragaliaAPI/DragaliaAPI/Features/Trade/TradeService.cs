﻿using DragaliaAPI.Features.Missions;
using DragaliaAPI.Features.Reward;
using DragaliaAPI.Features.Shop;
using DragaliaAPI.Models.Generated;
using DragaliaAPI.Services.Exceptions;
using DragaliaAPI.Shared.Definitions.Enums;
using DragaliaAPI.Shared.MasterAsset;
using DragaliaAPI.Shared.MasterAsset.Models;
using DragaliaAPI.Shared.MasterAsset.Models.Trade;
using Microsoft.EntityFrameworkCore;

namespace DragaliaAPI.Features.Trade;

public class TradeService(
    ITradeRepository tradeRepository,
    IRewardService rewardService,
    ILogger<TradeService> logger,
    IPaymentService paymentService,
    IMissionProgressionService missionProgressionService
) : ITradeService
{
    public IEnumerable<TreasureTradeList> GetCurrentTreasureTradeList()
    {
        DateTimeOffset current = DateTimeOffset.UtcNow;

        return MasterAsset
            .TreasureTrade.Enumerable.Where(x =>
                x.CompleteDate == DateTimeOffset.UnixEpoch || x.CompleteDate > current
            )
            .Select(trade => new TreasureTradeList
            {
                TreasureTradeId = trade.Id,
                Priority = trade.Priority,
                TabGroupId = trade.TabGroupId,
                CommenceDate = trade.CommenceDate,
                CompleteDate = trade.CompleteDate,
                IsLockView = trade.IsLockView == 1,
                ResetType = trade.ResetType,
                Limit = trade.Limit,
                DestinationEntityType = trade.DestinationEntityType,
                DestinationEntityId = trade.DestinationEntityId,
                DestinationEntityQuantity = trade.DestinationEntityQuantity,
                DestinationLimitBreakCount = trade.DestinationLimitBreakCount,
                NeedTradeEntityList = trade.NeedEntities.Select(x => new AtgenNeedTradeEntityList(
                    x.Type,
                    x.Id,
                    x.Quantity,
                    x.LimitBreakCount
                ))
            });
    }

    public IEnumerable<AbilityCrestTradeList> GetCurrentAbilityCrestTradeList()
    {
        DateTimeOffset current = DateTimeOffset.UtcNow;

        return MasterAsset
            .AbilityCrestTrade.Enumerable.Where(x =>
                x.CompleteDate == DateTimeOffset.UnixEpoch || x.CompleteDate > current
            )
            .Select(trade => new AbilityCrestTradeList
            {
                AbilityCrestTradeId = trade.Id,
                AbilityCrestId = trade.AbilityCrestId,
                CompleteDate = trade.CompleteDate,
                PickupViewStartDate = trade.PickupViewStartDate,
                PickupViewEndDate = trade.PickupViewEndDate,
                NeedDewPoint = trade.NeedDewPoint,
                Priority = trade.Priority
            });
    }

    public IEnumerable<EventTradeList> GetEventTradeList(int tradeGroupId)
    {
        return MasterAsset
            .EventTreasureTradeInfo.Enumerable.Where(x => x.TradeGroupId == tradeGroupId)
            .Select(x => new EventTradeList
            {
                EventTradeId = x.Id,
                TradeGroupId = x.TradeGroupId,
                TabGroupId = x.TabGroupId,
                Priority = x.Priority,
                IsLockView = x.IsLockView == 1,
                CommenceDate = x.CommenceDate,
                CompleteDate = x.CompleteDate,
                ResetType = x.ResetType,
                Limit = x.Limit,
                DestinationEntityType = x.DestinationEntityType,
                DestinationEntityId = x.DestinationEntityId,
                DestinationEntityQuantity = x.DestinationEntityQuantity,
                NeedEntityList = x
                    .NeedEntities.Where(y => y.Type != EntityTypes.None)
                    .Select(z => new AtgenBuildEventRewardEntityList(z.Type, z.Id, z.Quantity)),
                ReadStoryCount = 0,
                ClearTargetQuestId = 0
            });
    }

    public async Task<IEnumerable<AtgenUserEventTradeList>> GetUserEventTradeList()
    {
        return await tradeRepository
            .Trades.Where(x => x.Type == TradeType.Event)
            .Select(x => new AtgenUserEventTradeList(x.Id, x.Count))
            .ToListAsync();
    }

    public async Task<IEnumerable<UserAbilityCrestTradeList>> GetUserAbilityCrestTradeList()
    {
        return await tradeRepository
            .Trades.Where(x => x.Type == TradeType.AbilityCrest)
            .Select(x => new UserAbilityCrestTradeList(x.Id, x.Count))
            .ToListAsync();
    }

    public async Task<IEnumerable<UserTreasureTradeList>> GetUserTreasureTradeList()
    {
        return (await tradeRepository.GetTradesByTypeAsync(TradeType.Treasure)).Select(
            x => new UserTreasureTradeList(x.Id, x.Count, x.LastTradeTime)
        );
    }

    public async Task DoTrade(
        TradeType tradeType,
        int tradeId,
        int count,
        IEnumerable<AtgenNeedUnitList>? needUnitList = null
    )
    {
        logger.LogDebug(
            "Processing {tradeQuantity}x {tradeId} of trade type {tradeType}",
            count,
            tradeId,
            tradeType
        );

        TreasureTrade trade = tradeType switch
        {
            TradeType.None
                => throw new DragaliaException(
                    ResultCode.CommonDataValidationError,
                    "Invalid trade type none"
                ),
            TradeType.Treasure => MasterAsset.TreasureTrade[tradeId],
            TradeType.Event => MasterAsset.EventTreasureTradeInfo[tradeId],
            TradeType.AbilityCrest
                => throw new DragaliaException(
                    ResultCode.CommonInvalidArgument,
                    "Cannot process ability crest type in normal trade endpoint"
                ),
            _ => throw new DragaliaException(ResultCode.CommonInvalidArgument, "Invalid trade type")
        };

        foreach (
            (EntityTypes type, int id, int quantity, int limitBreakCount) in trade.NeedEntities
        )
        {
            if (type == EntityTypes.None)
                continue;

            await paymentService.ProcessPayment(
                new Entity(type, id, quantity * count, limitBreakCount)
            );
        }

        await rewardService.GrantReward(
            new(
                trade.DestinationEntityType,
                trade.DestinationEntityId,
                trade.DestinationEntityQuantity * count,
                trade.DestinationLimitBreakCount
            )
        );

        await tradeRepository.AddTrade(tradeType, tradeId, count, DateTimeOffset.UtcNow);

        int totalCount = (await tradeRepository.FindTrade(tradeId))?.Count ?? 0;

        missionProgressionService.OnTreasureTrade(
            tradeId,
            trade.DestinationEntityType,
            trade.DestinationEntityId,
            count,
            totalCount
        );
    }

    public async Task DoAbilityCrestTrade(int id, int count)
    {
        logger.LogDebug("Processing {tradeQuantity}x ability crest trade {tradeId}", count, id);

        if (count != 1)
            throw new DragaliaException(ResultCode.CommonDataValidationError, "Invalid count");

        AbilityCrestTrade trade = MasterAsset.AbilityCrestTrade[id];

        await paymentService.ProcessPayment(
            PaymentTypes.DewPoint,
            expectedPrice: trade.NeedDewPoint
        );

        await rewardService.GrantReward(
            new Entity(EntityTypes.Wyrmprint, (int)trade.AbilityCrestId)
        );

        await tradeRepository.AddTrade(TradeType.AbilityCrest, id, count);
    }
}
