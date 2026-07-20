using Jogo25D.Features.World.Properties.Resources;
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
                        existing.DamageMultiplier *= prop.DamageMultiplier;
                    }
                    else
                    {
                        result.Add(new DamagePropertyData
                        {
                            DamageType = prop.DamageType,
                            DamageAmount = prop.DamageAmount,
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

        public static MovementPropertyData Resolve(params List<MovementPropertyData>[] lists)
        {
            var result = new MovementPropertyData { Speed = 0f, JumpVelocity = 0f };

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    result.Speed += prop.Speed;
                    result.JumpVelocity += prop.JumpVelocity;
                }
            }

            return result;
        }

        public static HealthPropertyData Resolve(params List<HealthPropertyData>[] lists)
        {
            var result = new HealthPropertyData { MaxHealth = 0 };

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    result.MaxHealth += prop.MaxHealth;
                }
            }

            return result;
        }

        public static DashPropertyData Resolve(params List<DashPropertyData>[] lists)
        {
            var result = new DashPropertyData { DashSpeed = 0f, MovementInfluence = 0f };

            foreach (var list in lists)
            {
                foreach (var prop in list)
                {
                    result.DashSpeed = Math.Max(result.DashSpeed, prop.DashSpeed);
                    result.MovementInfluence = Math.Max(result.MovementInfluence, prop.MovementInfluence);
                }
            }

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

        public static BasePropertyData CloneProperty(BasePropertyData property)
        {
            return property switch
            {
                DamagePropertyData p => new DamagePropertyData { DamageAmount = p.DamageAmount, DamageType = p.DamageType, DamageMultiplier = p.DamageMultiplier },
                AttackPropertyData p => new AttackPropertyData { AttackRange = p.AttackRange, AttackArea = p.AttackArea, KnockbackForce = p.KnockbackForce, ProjectileSpeed = p.ProjectileSpeed },
                ChargesPropertyData p => new ChargesPropertyData { MaxCharges = p.MaxCharges, ChargeItemId = p.ChargeItemId, InfiniteCharges = p.InfiniteCharges, ReloadCooldown = p.ReloadCooldown },
                CritPropertyData p => new CritPropertyData { CritChance = p.CritChance, CritDamage = p.CritDamage },
                DamageResistencePropertyData p => new DamageResistencePropertyData { DamageType = p.DamageType, ResistanceFactor = p.ResistanceFactor },
                DamageResistenceMultiplierPropertyData p => new DamageResistenceMultiplierPropertyData { DamageType = p.DamageType, Multiplier = p.Multiplier },
                DashPropertyData p => new DashPropertyData { DashSpeed = p.DashSpeed, MovementInfluence = p.MovementInfluence },
                MovementPropertyData p => new MovementPropertyData { Speed = p.Speed, JumpVelocity = p.JumpVelocity },
                HealthPropertyData p => new HealthPropertyData { MaxHealth = p.MaxHealth },
                _ => property
            };
        }

        public static Godot.Collections.Array<BasePropertyData> CloneProperties(IEnumerable<BasePropertyData> properties)
        {
            var result = new Godot.Collections.Array<BasePropertyData>();

            foreach (var property in properties)
            {
                result.Add(CloneProperty(property));
            }

            return result;
        }
    }
}