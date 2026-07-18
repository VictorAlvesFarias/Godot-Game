using Jogo25D.Items;
using Jogo25D.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Features.World.Resolver.Singletons
{
    public static class Resolver
    {
        public static List<DamagePropertyData> Resolve(params List<DamagePropertyData>[] lists)
        {
            var result = new List<DamagePropertyData>();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    var existing = result.FirstOrDefault(p => p.DamageType == prop.DamageType);

                    if (existing != null)
                    {
                        existing.DamageAmount += prop.DamageAmount;
                    }
                    else
                    {
                        result.Add(new DamagePropertyData
                        {
                            DamageType = prop.DamageType,
                            DamageAmount = prop.DamageAmount
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageMultiplierPropertyData> Resolve(params List<DamageMultiplierPropertyData>[] lists)
        {
            var result = new List<DamageMultiplierPropertyData>();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    var existing = result.FirstOrDefault(p => p.DamageType == prop.DamageType);

                    if (existing != null)
                    {
                        existing.DamageMultiplier *= prop.DamageMultiplier;
                    }
                    else
                    {
                        result.Add(new DamageMultiplierPropertyData
                        {
                            DamageType = prop.DamageType,
                            DamageMultiplier = prop.DamageMultiplier
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageResistencePropertyData> Resolve(params List<DamageResistencePropertyData>[] lists)
        {
            var result = new List<DamageResistencePropertyData>();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    var existing = result.FirstOrDefault(p => p.DamageType == prop.DamageType);

                    if (existing != null)
                    {
                        existing.ResistanceFactor = Math.Clamp(existing.ResistanceFactor + prop.ResistanceFactor, 0f, 1f);
                    }
                    else
                    {
                        result.Add(new DamageResistencePropertyData
                        {
                            DamageType = prop.DamageType,
                            ResistanceFactor = Math.Clamp(prop.ResistanceFactor, 0f, 1f)
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageResistenceMultiplierPropertyData> Resolve(params List<DamageResistenceMultiplierPropertyData>[] lists)
        {
            var result = new List<DamageResistenceMultiplierPropertyData>();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    var existing = result.FirstOrDefault(p => p.DamageType == prop.DamageType);

                    if (existing != null)
                    {
                        existing.Multiplier *= prop.Multiplier;
                    }
                    else
                    {
                        result.Add(new DamageResistenceMultiplierPropertyData
                        {
                            DamageType = prop.DamageType,
                            Multiplier = prop.Multiplier
                        });
                    }
                }
            }

            return result;
        }
        
        public static List<ChargesPropertyData> Resolve(params List<ChargesPropertyData>[] lists)
        {
            var result = new List<ChargesPropertyData>();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    var existing = result.FirstOrDefault(p => p.ChargeItemId == prop.ChargeItemId);

                    if (existing != null)
                    {
                        existing.MaxCharges     += prop.MaxCharges;
                        existing.ReloadCooldown = Math.Min(existing.ReloadCooldown, prop.ReloadCooldown);
                        existing.InfiniteCharges = existing.InfiniteCharges || prop.InfiniteCharges;
                    }
                    else
                    {
                        result.Add(new ChargesPropertyData
                        {
                            ChargeItemId = prop.ChargeItemId,
                            MaxCharges = prop.MaxCharges,
                            ReloadCooldown = prop.ReloadCooldown,
                            InfiniteCharges = prop.InfiniteCharges
                        });
                    }
                }
            }

            return result;
        }

        public static CritPropertyData Resolve(params List<CritPropertyData>[] lists)
        {
            var result = new CritPropertyData();

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    result.CritChance += prop.CritChance;
                    result.CritDamage += prop.CritDamage;
                }
            }

            result.CritChance = Math.Clamp(result.CritChance, 0f, 1f);

            return result;
        }

        public static AttackPropertyData Resolve(params List<AttackPropertyData>[] lists)
        {
            var result = new AttackPropertyData
            {
                AttackRange = 0f,
                AttackArea = 0f,
                KnockbackForce = 0f,
                ProjectileSpeed = 0f
            };

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    result.AttackRange = Math.Max(result.AttackRange,     prop.AttackRange);
                    result.AttackArea = Math.Max(result.AttackArea,      prop.AttackArea);
                    result.KnockbackForce = Math.Max(result.KnockbackForce,  prop.KnockbackForce);
                    result.ProjectileSpeed = Math.Max(result.ProjectileSpeed, prop.ProjectileSpeed);
                }
            }

            return result;
        }
    }
}