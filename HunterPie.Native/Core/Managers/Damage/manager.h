#pragma once
#include <unordered_map>
#include "Core/Entities/damage.h"

using namespace Core::Entities;

namespace HunterPie::Core::Damage
{
    // Indices 0-3 players, 4-5 followers, 10-13 pets (owner+10). Keep >= 14.
    constexpr size_t kMaxTrackedEntities = 14;

    struct HuntStatistics
    {
        EntityDamageData entities[kMaxTrackedEntities];
    };

    class DamageTrackManager
    {
    private:
        std::unordered_map<intptr_t, HuntStatistics*> m_Trackings;
        HuntStatistics m_AllTargetsTotal;
        static DamageTrackManager* m_Instance;
        DamageTrackManager();
        DamageTrackManager operator=(DamageTrackManager const&);
        EntityDamageData* CalculateTotalDamage();

    public:
        static DamageTrackManager* GetInstance();

        void UpdateDamage(const EntityDamageData& damageData);
        HuntStatistics* GetHuntStatisticsBy(intptr_t target);
        void DeleteBy(intptr_t target);
        void ClearAllExcept(intptr_t* exceptions, size_t length);
    };
}
