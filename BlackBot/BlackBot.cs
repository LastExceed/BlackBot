using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Resources;
using Resources.Packet;
using Resources.Utilities;
using System.Threading;
using System.Diagnostics;

namespace ETbot {
    static class BlackBot {
        static TcpClient connection;
        static BinaryReader reader;
        static BinaryWriter writer;
        static long personalGuid;
        static Dictionary<long, EntityUpdate> players;
        static Stopwatch swLasthittime;
        static Stopwatch swCooldown;
        static long maloxGuid;
        static byte[] firespammers;
        static string previousMessage;
        struct Chunk{
            public int x;
            public int y;
        }
        static Dictionary<Chunk, List<ServerUpdate.ChunkItems.DroppedItem>> drops = new Dictionary<Chunk, List<ServerUpdate.ChunkItems.DroppedItem>>();
        static string password;

        public static void Connect(string hostname, int port, string pw) {
            swLasthittime = new Stopwatch();
            swCooldown = new Stopwatch();
            firespammers = new byte[1024];

            password = pw;
            players = new Dictionary<long, EntityUpdate>();
            connection = new TcpClient(hostname, port);
            var thatStream = connection.GetStream();
            reader = new BinaryReader(thatStream);
            writer = new BinaryWriter(thatStream);

            var zumServerhallosagen = new ProtocolVersion {
                version = 3,
            };
            zumServerhallosagen.Write(writer);
            swCooldown.Start();
            while (true) {
                var packetid = reader.ReadInt32();
                //Console.WriteLine(packetid);
                ProcessPacket(packetid);
            }
        }

        static void ProcessPacket(int packetid) {
            switch (packetid) {
                case 0:
                    #region entityUpdate
                    var entityUpdate = new EntityUpdate(reader);
                    if (players.ContainsKey(entityUpdate.guid)) {
                        var previous = players[entityUpdate.guid];
                        if (entityUpdate.modeTimer == 0) {
                            if ((entityUpdate.mode == Mode.FireExplosion_After || (entityUpdate.mode == null && previous.mode == Mode.FireExplosion_After)) && previous.modeTimer < 1000) {
                                firespammers[entityUpdate.guid]++;
                                switch (firespammers[entityUpdate.guid]) {
                                    case 1://nothing
                                        break;
                                    case 2://warn
                                        new ChatMessage {
                                            message = "/pm #" + entityUpdate.guid + " stop spamming fire explosion, its a bannable abuse"
                                        }.Write(writer);
                                        break;
                                    default://kick
                                        new ChatMessage {
                                            message = "/kick #" + entityUpdate.guid + " firespamming (black_bot)"
                                        }.Write(writer);
                                        break;
                                }
                            }
                            else {
                                //firespammers[entityUpdate.guid] = 0;
                            }
                        }
                        entityUpdate.Merge(players[entityUpdate.guid]);
                    }
                    else {
                        players.Add(entityUpdate.guid, entityUpdate);
                    }
                    if (entityUpdate.MP > 1f) {
                        new ChatMessage {
                            message = "/kick #" + entityUpdate.guid + " MP lock (black_bot)"
                        }.Write(writer);
                    }
                    if (entityUpdate.name != null) {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        //Console.WriteLine(entityUpdate.guid + ": " + entityUpdate.name);
                    }

                    if (false && players[entityUpdate.guid].name.ToLower() == "²@blackrock") {
                        maloxGuid = entityUpdate.guid;
                        var opplayer = new EntityUpdate();
                        var x = players[entityUpdate.guid].position.x - players[personalGuid].position.x;
                        var y = players[entityUpdate.guid].position.y - players[personalGuid].position.y;
                        double distance = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                        if (distance > 65536 * 40) {
                            var follow = new EntityUpdate {
                                position = players[entityUpdate.guid].position,
                                guid = personalGuid
                            };
                            follow.Write(writer);
                        }
                        if (entityUpdate.modeTimer < 50) {
                            var shoot = new Shoot() {
                                attacker = personalGuid,
                                chunkX = (int)players[personalGuid].position.x / 0x1000000,
                                chunkY = (int)players[personalGuid].position.y / 0x1000000,
                                position = players[personalGuid].position,
                                particles = 1f,
                                mana = 1f,
                                scale = 5f,
                                projectile = ProjectileType.Arrow,
                            };
                            shoot.position.x = players[maloxGuid].position.x + (long)(players[maloxGuid].rayHit.x * 0x10000);
                            shoot.position.y = players[maloxGuid].position.y + (long)(players[maloxGuid].rayHit.y * 0x10000);
                            shoot.position.z = players[maloxGuid].position.z + (long)((players[maloxGuid].rayHit.z + 15) * 0x10000);

                            shoot.velocity.z = -40f;

                            //shoot.velocity.x = (float)players[maloxGuid].position.x / 0x10000f + players[maloxGuid].rayHit.x - (float)players[personalGuid].position.x / 0x10000f;
                            //shoot.velocity.y = (float)players[maloxGuid].position.y / 0x10000f + players[maloxGuid].rayHit.y - (float)players[personalGuid].position.y / 0x10000f;


                            //shoot.velocity.z = (float)players[maloxGuid].position.z / 0x10000f + players[maloxGuid].rayHit.z - (float)players[personalGuid].position.z / 0x10000f;
                            int range = 10;
                            shoot.position.x -= (range - 1) / 2 * 0x10000;
                            shoot.position.y -= (range - 1) / 2 * 0x10000;
                            for (int i = 0; i < range; i++) {
                                for (int j = 0; j < range; j++) {
                                    shoot.Write(writer);
                                    shoot.position.x += 0x10000;
                                }
                                shoot.position.x -= range * 0x10000;
                                shoot.position.y += 0x10000;
                            }
                        }
                    }
                    goto case 2;//break;
                #endregion
                case 2:
                    #region complete
                    var antiTimeout = new EntityUpdate() {
                        guid = personalGuid,
                        position = players[personalGuid].position,
                        lastHitTime = (int)swLasthittime.ElapsedMilliseconds
                    };
                    antiTimeout.Write(writer);
                    break;
                #endregion
                case 4:
                    #region serverupdate
                    var serverUpdate = new ServerUpdate(reader);
                    foreach (var hit in serverUpdate.hits) {
                        if (hit.attacker == hit.target) continue;
                        Console.WriteLine(hit.attacker + " attacked " + hit.target + " with " + hit.damage);
                        if (hit.damage > 500f && players[hit.attacker].entityClass == EntityClass.Rogue) {
                            SendMessage("/kick #" + hit.attacker + " shuriken glitch (black_bot)");
                        }
                        if (hit.target == personalGuid) {
                            swLasthittime.Restart();
                            if (players[personalGuid].HP <= 0) continue;
                            players[personalGuid].HP -= hit.damage / 2;
                            var life = new EntityUpdate() {
                                guid = personalGuid,
                                HP = players[personalGuid].HP,
                                lastHitTime = (int)swLasthittime.ElapsedMilliseconds
                            };
                            life.Write(writer);
                            if (players[personalGuid].HP <= 0) {
                                SendMessage("/firework");
                                life.HP = players[personalGuid].HP = 3000f;
                                life.lastHitTime = 0;
                                life.Write(writer);
                                swLasthittime.Restart();
                            }
                        }
                    }
                    foreach (var chunkItemData in serverUpdate.chunkItems) {
                        var c = new Chunk() { x = chunkItemData.chunkX, y = chunkItemData.chunkY };
                        if (!drops.ContainsKey(c)) {
                            drops.Add(c, chunkItemData.droppedItems);
                        }
                        else {
                            drops[c] = chunkItemData.droppedItems;
                        }
                    }
                    break;
                #endregion
                case 5:
                    #region time
                    var time = new Time(reader);
                    break;
                #endregion
                case 10:
                    #region chat
                    var chatMessage = new ChatMessage(reader, true);
                    long sender = (long)chatMessage.sender;
                    if (sender == 0) {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    else {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(players[sender].name + ": ");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    Console.WriteLine(chatMessage.message);
                    switch (chatMessage.message.ToLower()) {
                        case ".heal_me":
                        case ".kill_me":
                            SendMessage("/pm #" + sender + " This command is no longer available");
                            break;
                        case ".stun_me":
                            new Hit {
                                attacker = personalGuid,
                                target = sender,
                                damage = 0f,
                                critical = true,
                                stuntime = 10000,
                                showlight = true,
                                position = players[sender].position
                            }.Write(writer);
                            SendMessage("/pm #" + sender + " have fun sticking to the ground for 10 seconds :P");
                            break;
                        case ".shutdown":
                            if (chatMessage.message == ".shutdOWN") {
                                SendMessage("goodbye");
                                Task.Delay(250).Wait();
                                Environment.Exit(0);
                            }
                            else SendMessage("no permission");
                            break;
                        case ".clear":
                            foreach (var kvp in drops) {
                                for (int i = 0; i < kvp.Value.Count; i++) {
                                    var pickup = new EntityAction() {
                                        type = ActionType.PickUp,
                                        chunkX = kvp.Key.x,
                                        chunkY = kvp.Key.y,
                                        index = i,
                                        item = kvp.Value[i].item,
                                    };
                                    pickup.Write(writer);
                                    pickup.Write(writer);
                                }
                            }
                            //SendMessage(".\n\n\n\n\n\n\n\n\n\n.");
                            break;

                        case ".items":
                            #region items
                            double timePassed = swCooldown.ElapsedMilliseconds / 1000f;
                            if (timePassed < 30f) {
                                SendMessage("/pm #" + sender + " command is on cooldown (30sec)");
                                break;
                            }
                            bool fullyGeared = true;
                            var pl = players[sender];
                            for (int i = 1; i < 9; i++) {
                                if (pl.equipment[i].level < pl.level || pl.equipment[i].rarity != ItemRarity.Legendary) {
                                    fullyGeared = false;
                                    break;
                                }
                            }
                            if (pl.equipment[10].type == 0 || pl.equipment[11].type == 0) fullyGeared = false;
                            if (fullyGeared) {
                                SendMessage("/pm #" + chatMessage.sender + " you already have maximum gear, don't spam items.");
                                break;
                            }
                            swCooldown.Restart();

                            var port = new EntityUpdate {
                                position = players[sender].position,
                                guid = personalGuid
                            };
                            port.Write(writer);
                            players[personalGuid].position = port.position;

                            var items = new List<Item>();

                            var rng = new Random();
                            for (byte i = 3; i <= 9; i++) {
                                items.Add(new Item() {
                                    type = (ItemType)i,
                                    modifier = rng.Next(0x7FFFFFFF),
                                    rarity = ItemRarity.Legendary,
                                    level = (short)players[sender].level
                                });
                            }
                            items[5].material = (ItemMaterial)rng.Next(11, 12);
                            items[6].material = (ItemMaterial)rng.Next(11, 12);
                            items.Add(items[6]);
                            ItemMaterial armorMaterial;
                            switch (players[sender].entityClass) {
                                case EntityClass.Warrior:
                                        //items[0].subtype = 0;
                                    items[0].material = ItemMaterial.Iron;
                                    for (int i = 0; i < 6; i++) {
                                        items.Add(new Item() {
                                            type = ItemType.Weapon,
                                            material = ItemMaterial.Iron,
                                            modifier = rng.Next(0x7FFFFFFF),
                                            rarity = ItemRarity.Legendary,
                                            level = (short)players[sender].level
                                        });
                                    }
                                    items[8].subtype = 1;
                                    items[9].subtype = 2;
                                    items[10].subtype = 13;
                                    items[11].subtype = 15;
                                    items[12].subtype = 16;
                                    items[13].subtype = 17;

                                    armorMaterial = ItemMaterial.Iron;
                                    break;

                                case EntityClass.Ranger:
                                    items[0].subtype = 6;
                                    items[0].material = ItemMaterial.Wood;
                                    for (int i = 0; i < 2; i++) {
                                        items.Add(new Item() {
                                            type = ItemType.Weapon,
                                            material = ItemMaterial.Wood,
                                            modifier = rng.Next(0x7FFFFFFF),
                                            rarity = ItemRarity.Legendary,
                                            level = (short)players[sender].level
                                        });
                                    }
                                    items[8].subtype = 7;
                                    items[9].subtype = 8;

                                    armorMaterial = ItemMaterial.Linen;
                                    break;

                                case EntityClass.Mage:
                                    items[0].subtype = 10;
                                    items[0].material = ItemMaterial.Wood;
                                    for (int i = 0; i < 3; i++) {
                                        items.Add(new Item() {
                                            type = ItemType.Weapon,
                                            material = (ItemMaterial)rng.Next(11, 12),
                                            modifier = rng.Next(0x7FFFFFFF),
                                            rarity = ItemRarity.Legendary,
                                            level = (short)players[sender].level
                                        });
                                    }

                                    items[8].subtype = 11;
                                    items[8].material = ItemMaterial.Wood;
                                    items[9].subtype = 12;
                                    items[10].subtype = 12;

                                    armorMaterial = ItemMaterial.Silk;
                                    break;

                                case EntityClass.Rogue:
                                    items[0].subtype = 3;
                                    items[0].material = ItemMaterial.Iron;
                                    for (int i = 0; i < 2; i++) {
                                        items.Add(new Item() {
                                            type = ItemType.Weapon,
                                            material = ItemMaterial.Iron,
                                            modifier = rng.Next(0x7FFFFFFF),
                                            rarity = ItemRarity.Legendary,
                                            level = (short)players[sender].level
                                        });
                                    }
                                    items[8].subtype = 4;
                                    items[9].subtype = 5;

                                    armorMaterial = ItemMaterial.Cotton;
                                    break;

                                default:
                                    goto case EntityClass.Warrior;
                            }
                            for (int i = 1; i <= 4; i++) {
                                items[i].material = armorMaterial;
                            }

                            items.Add(new Item {
                                type = ItemType.Special,
                                material = ItemMaterial.Wood,
                            });

                            var drop = new EntityAction {
                                type = ActionType.Drop
                            };
                            foreach (var that in items) {
                                drop.item = that;
                                drop.Write(writer);
                            }
                            SendMessage("items delivered");
                            #endregion
                            break;
                        case ".come_here":
                            var port2 = new EntityUpdate {
                                position = players[sender].position,
                                guid = personalGuid
                            };
                            port2.Write(writer);
                            break;
                        case ".info":
                        case ".help":
                            SendMessage("hi " + players[sender].name + "! I am a computer controlled player, created by Malox and @Blackrock. I can do various stuff, type .commands for more");
                            break;
                        case ".commands":
                            SendMessage(".info .commands .items .countdown come_here .stun_me .69 .restart .come_here");
                            break;
                        case ".countdown":
                            Task.Factory.StartNew(Countdown);
                            break;
                        case ".killall":
                            if (chatMessage.message == ".kiLLaLL") {
                                foreach (var p in players.Values) {
                                    if (p.guid < 1000 && p.guid > 2) {
                                        new EntityUpdate {
                                            position = p.position,
                                            guid = personalGuid
                                        }.Write(writer);
                                        Task.Delay(100).Wait();
                                        new Hit {
                                            attacker = personalGuid,
                                            target = p.guid,
                                            damage = 10000f,
                                        }.Write(writer);
                                        Task.Delay(100).Wait();
                                    }
                                }
                            }
                            else {
                                SendMessage("haha you wish!");
                            }
                            break;
                        case ".69":
                            new EntityUpdate {
                                position = players[sender].position,
                                guid = personalGuid
                            }.Write(writer);

                            var sixtynine = new Hit {
                                attacker = personalGuid,
                                target = sender,
                                damage = 138f,//-0.2f,
                                critical = true,
                                showlight = true,
                                position = players[sender].position
                            };
                            for (int i = 0; i < 30; i++) {
                                sixtynine.damage *= -1;
                                sixtynine.Write(writer);
                                Task.Delay(50).Wait();
                            }
                            break;

                        case ".derp":// ".idontevenknow":
                            if (chatMessage.message == ".dERp") {
                                var tele = new EntityUpdate {
                                    position = new LongVector() { z = players[sender].position.z },
                                    guid = personalGuid
                                };

                                var dropperino = new EntityAction {
                                    type = ActionType.Drop,
                                    item = new Item() {
                                        type = ItemType.PetFood,//1,
                                        subtype = 19,//1,
                                        material = 0,
                                        level = 1,
                                    },
                                    
                                };
                                for (int i = 0; i < 25; i++) {
                                    for (int j = 0; j < 25; j++) {
                                        tele.position.x = players[sender].position.x + (i * 90000);
                                        tele.position.y = players[sender].position.y + (j * 90000);
                                        tele.Write(writer);
                                        dropperino.Write(writer);
                                    }
                                }
                            }
                            else {
                                SendMessage("no permission");
                            }
                            break;

                        case ".restart":
                            SendMessage("I'll be right back");
                            throw new Exception();

                        case ".boost":
                            if (chatMessage.message == ".bOOst") {
                                var dayum = new Hit {
                                    attacker = personalGuid,
                                    target = sender,
                                    damage = -10000f,
                                    critical = true,
                                    showlight = true,
                                    position = players[sender].position
                                };
                                for (int i = 0; i < 30; i++) {
                                    dayum.Write(writer);
                                    SendMessage("/firework");
                                    Task.Delay(50).Wait();
                                }
                            }
                            else {
                                SendMessage("no permission");
                            }
                            break;
                        case ".ping":
                            SendMessage("pong!");
                            break;

                        default:
                            break;
                    }
                    if (chatMessage.message.Contains("change") && chatMessage.message.Contains("team") && chatMessage.sender != personalGuid) SendMessage("./team_join red/blue");
                    break;
                #endregion
                case 15:
                    #region mapseed
                    var mapSeed = new MapSeed(reader);
                    break;
                #endregion
                case 16:
                    #region join
                    var join = new Join(reader);
                    personalGuid = join.guid;
                    var playerstats = new EntityUpdate() {
                        position = new LongVector() {
                            x = 550299161554,//8020800000,
                            y = 550289388106,//8020800000,
                            z = 6296719,
                        },
                        rotation = new FloatVector(),
                        velocity = new FloatVector(),
                        acceleration = new FloatVector(),
                        extraVel = new FloatVector(),
                        viewportPitch = 0,
                        physicsFlags = 0b00000000_00000000_00000000_00010001,//17
                        hostility = 0,
                        entityType = EntityType.HumanMale,
                        mode = 0,
                        modeTimer = 0,
                        combo = 0,
                        lastHitTime = 0,
                        appearance = new EntityUpdate.Appearance() {
                            character_size = new FloatVector() {
                                x = 0.9600000381f,
                                y = 0.9600000381f,
                                z = 2.160000086f
                            },
                            head_model = 1249,
                            hair_model = 1265,
                            hand_model = 431,
                            foot_model = 432,
                            body_model = 1,
                            tail_model = -1,
                            shoulder2_model = -1,
                            wings_model = -1,
                            head_size = 1.00999999f,
                            body_size = 1f,
                            hand_size = 1f,
                            foot_size = 0.9800000191f,
                            shoulder2_size = 1f,
                            weapon_size = 0.9499999881f,
                            tail_size = 0.8000000119f,
                            shoulder_size = 1f,
                            wings_size = 1f,
                            body_offset = new FloatVector() {
                                z = -5f
                            },
                            head_offset = new FloatVector() {
                                y = 0.5f,
                                z = 5f
                            },
                            hand_offset = new FloatVector() {
                                x = 6f,
                            },
                            foot_offset = new FloatVector() {
                                x = 3f,
                                y = 1f,
                                z = -10.5f
                            },
                            back_offset = new FloatVector() {
                                y = -8f,
                                z = 2f
                            },
                        },
                        entityFlags = 0b00000000_00000000_00000000_00100000,//64
                        roll = 0,
                        stun = 0,
                        slow = 0,
                        ice = 0,
                        wind = 0,
                        showPatchTime = 0,
                        entityClass = EntityClass.Warrior,
                        specialization = 1,
                        charge = 0,
                        unused24 = new FloatVector(),
                        unused25 = new FloatVector(),
                        rayHit = new FloatVector(),
                        HP = 3000f,
                        MP = 0,
                        block = 1,
                        multipliers = new EntityUpdate.Multipliers() {
                            HP = 100,
                            attackSpeed = 1,
                            damage = 1,
                            armor = 1,
                            resi = 1,
                        },
                        unused31 = 0,
                        unused32 = 0,
                        level = 500,
                        XP = 0,
                        parentOwner = 0,
                        unused36 = 0,
                        powerBase = 0,
                        unused38 = 0,
                        unused39 = new IntVector(),
                        spawnPos = new LongVector(),
                        unused41 = new IntVector(),
                        unused42 = 0,
                        consumable = new Item(),
                        equipment = new Item[13],
                        name = "@BLACK_BOT",
                        skillDistribution = new EntityUpdate.SkillDistribution() {
                            ability1 = 5,
                            ability2 = 5,
                            ability3 = 5,
                        },
                        manaCubes = 0,
                    };
                    for (int i = 0; i < 13; i++) {
                        playerstats.equipment[i] = new Item() {
                        };
                    }
                    var e = playerstats.equipment;
                    e[1].type = ItemType.Amulet;
                    e[1].modifier = 3;
                    e[1].rarity = ItemRarity.Legendary;
                    e[1].material = ItemMaterial.Silver;
                    e[1].level = 647;

                    e[2].type = ItemType.Chest;
                    e[2].modifier = 63;
                    e[2].rarity = ItemRarity.Epic;
                    e[2].material = ItemMaterial.Iron;
                    e[2].level = 647;

                    e[3].type = ItemType.Boots;
                    e[3].modifier = 63;
                    e[3].rarity = ItemRarity.Legendary;
                    e[3].material = ItemMaterial.Iron;
                    e[3].level = 647;

                    e[4].type = ItemType.Gloves;
                    e[4].modifier = 0;
                    e[4].rarity = ItemRarity.Legendary;
                    e[4].material = ItemMaterial.Iron;
                    e[4].level = 647;

                    e[5].type = ItemType.Shoulder;
                    e[5].modifier = 84;
                    e[5].rarity = ItemRarity.Epic;
                    e[5].material = ItemMaterial.Iron;
                    e[5].level = 647;

                    e[6].type = ItemType.Weapon;
                    e[6].subtype = 2;
                    e[6].modifier = -67;
                    e[6].rarity = ItemRarity.Epic;
                    e[6].material = ItemMaterial.Iron;
                    e[6].level = 647;

                    e[7] = e[6];

                    playerstats.Write(writer);
                    swLasthittime.Start();
                    players.Add(personalGuid, playerstats);

                    SendMessage("/login " + password);
                    SendMessage("/trail 0 0 0");
                    SendMessage("online (version 3.4.1)");
                    break;
                #endregion
                case 17: //serving sending the right version if yours is wrong
                    #region version
                    var version = new ProtocolVersion(reader);
                    break;
                #endregion
                case 18:
                    #region server full
                    //empty
                    break;
                #endregion
                default:
                    Console.WriteLine(string.Format("unknown packet id: {0}", packetid));
                    break;
            }
        }
        static void SendMessage(string message) {
            if (!message.StartsWith("/") && message == previousMessage) {
                message += "'";
            }
            previousMessage = message;
            new ChatMessage {
                //sender = personalGuid,
                message = message
            }.Write(writer);
        }
        static void Countdown() {
            for (sbyte x = 3; x > 0; x--) {
                SendMessage("" + x);
                Task.Delay(1000).Wait();
            }
            SendMessage("go!");
        }
    }
}
