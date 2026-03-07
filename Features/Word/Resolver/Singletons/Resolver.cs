using Jogo25D.Items;
using Jogo25D.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Features.Word.Resolver.Singletons
{
    public static class Resolver
    {
        public static List<DamageProperty> Resolve(params List<DamageProperty>[] lists)
        {
            var result = new List<DamageProperty>();

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
                        result.Add(new DamageProperty
                        {
                            DamageType = prop.DamageType,
                            DamageAmount = prop.DamageAmount
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageMultiplierProperty> Resolve(params List<DamageMultiplierProperty>[] lists)
        {
            var result = new List<DamageMultiplierProperty>();

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
                        result.Add(new DamageMultiplierProperty
                        {
                            DamageType = prop.DamageType,
                            DamageMultiplier = prop.DamageMultiplier
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageResistenceProperty> Resolve(params List<DamageResistenceProperty>[] lists)
        {
            var result = new List<DamageResistenceProperty>();

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
                        result.Add(new DamageResistenceProperty
                        {
                            DamageType = prop.DamageType,
                            ResistanceFactor = Math.Clamp(prop.ResistanceFactor, 0f, 1f)
                        });
                    }
                }
            }

            return result;
        }

        public static List<DamageResistenceMultiplierProperty> Resolve(params List<DamageResistenceMultiplierProperty>[] lists)
        {
            var result = new List<DamageResistenceMultiplierProperty>();

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
                        result.Add(new DamageResistenceMultiplierProperty
                        {
                            DamageType = prop.DamageType,
                            Multiplier = prop.Multiplier
                        });
                    }
                }
            }

            return result;
        }
        
        public static List<ChargesProperty> Resolve(params List<ChargesProperty>[] lists)
        {
            var result = new List<ChargesProperty>();

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
                        result.Add(new ChargesProperty
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

        public static CritProperty Resolve(params List<CritProperty>[] lists)
        {
            var result = new CritProperty();

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

        public static AttackProperty Resolve(params List<AttackProperty>[] lists)
        {
            var result = new AttackProperty
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