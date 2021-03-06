﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Dapper.FastCrud;
using Netsphere.Database.Game;
using Netsphere.Network;
using Netsphere.Network.Data.Game;
using Netsphere.Network.Message.Game;
using Netsphere.Resource;
using Serilog;
using Serilog.Core;

namespace Netsphere
{
    internal class WeeklyMission
    {
        private const int MaxRuningTask = 3;

        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(WeeklyMission));
        private Dictionary<uint, PlayerMissionsDto> _tasksR;
        private Dictionary<uint, ushort> _taskOnGoing;
        private int _rMRunning;
        private int _wMRunning;

        public Player Player { get; set; }

        public WeeklyMission(Player plr, PlayerDto plrDto)
        {
            int _rMissions;
            int _wMissions;
            Player = plr;
            _tasksR = new Dictionary<uint, PlayerMissionsDto>();
            _taskOnGoing = new Dictionary<uint, ushort>();

            var TaskList = GameServer.Instance.ResourceCache.GetTasks();

            _rMissions = 0;
            _rMRunning = 0;
            _wMissions = 0;
            _wMRunning = 0;

            foreach (var task in plrDto.Missions)
            {
                var t = TaskList[task.TaskId];

                var TaskDate = DateTime.FromBinary(task.Date);
                var today = DateTime.Now.DayOfWeek - DayOfWeek.Monday;
                var dt = DateTime.Now.AddDays(-today);

                if (t.constant || dt.Subtract(TaskDate).Days < 7)
                {
                    if (t.constant)
                    {
                        if (task.Progress == t.EndCondition.repetetion)
                            _rMissions++;
                        else
                            _rMRunning++;
                    }
                    else
                    {
                        if (task.Progress == t.EndCondition.repetetion)
                            _wMissions++;
                        else
                            _wMRunning++;
                    }

                    _tasksR.Add(task.TaskId, task);
                    Logger.ForAccount(Player)
                            .Debug($"Mission {task.Id} - {t.Name}, progress {task.Progress}/{t.EndCondition.repetetion}");
                }
            }

            var rand = new Random();

            if (_rMissions + _rMRunning == 0)
            {
                var RCMissionList = from t in TaskList
                                    where t.Value.Level == 0
                                    && t.Value.constant && t.Value.StartCondition.CanStart(plr)
                                    orderby t.Value.Level ascending
                                    select t.Value;

                while (_rMRunning < MaxRuningTask)
                {
                    foreach (var task in RCMissionList)
                    {
                        if (task.GetChance(plr) < rand.Next(100)
                            || _tasksR.ContainsKey(task.Id))
                            continue;
                        if (_rMRunning >= MaxRuningTask)
                            break;

                        var rt = (task.RewardExp != 0) ? rand.Next(1, 2) : 1;

                        _tasksR.Add(task.Id, new PlayerMissionsDto
                        {
                            TaskId = task.Id,
                            PlayerId = plr.Account.Id,
                            Slot = (byte)(_rMRunning%3),
                            Progress = 0,
                            Date = DateTime.Now.ToBinary(),
                            RewardType = (byte)rt,
                            Reward = (ushort)(rt == 1 ? task.RewardPen : task.RewardExp)
                        });

                        Logger.ForAccount(Player)
                            .Debug($"Added Mission {task.Id} - {task.Level} - {task.Name}");

                        _rMRunning++;
                    }
                }
            }

            if (_wMissions + _wMRunning == 0 && _rMissions == 12)
            {
                var RCMissionList = from t in TaskList
                                    where t.Value.Level == 0
                                    && t.Value.constant && t.Value.StartCondition.CanStart(plr)
                                    orderby t.Value.Level ascending
                                    select t.Value;
                while (_wMRunning < MaxRuningTask)
                {
                    foreach (var task in RCMissionList)
                    {
                        if (_wMRunning >= MaxRuningTask)
                            break;
                        if (task.GetChance(plr) < rand.Next(100)
                            || _tasksR.ContainsKey(task.Id))
                            continue;

                        var rt = (task.RewardExp != 0) ? rand.Next(1,2) : 1;

                        _tasksR.Add(task.Id, new PlayerMissionsDto
                        {
                            TaskId = task.Id,
                            PlayerId = plr.Account.Id,
                            Progress = 0,
                            Date = DateTime.Now.ToBinary(),
                            Slot = (byte)(_wMRunning % 3),
                            RewardType = (byte)rt,
                            Reward = (ushort)(rt == 1 ? task.RewardPen : task.RewardExp)
                        });

                        Logger.ForAccount(Player)
                            .Debug($"Weekly Added Mission {task.Id} - {task.Level} - {task.Name}");

                        _wMRunning++;
                    }
                }
            }
        }

        public TaskDto[] GetTasks()
        {
            var i = 0;

            var result = new List<TaskDto>();

            foreach (var taskKeyPair in _tasksR)
            {
                var task = taskKeyPair.Value;
                result.Add(new TaskDto
                {
                    Id = task.TaskId,
                    Unk1 = task.Slot, //Slot?
                    Progress = (ushort)task.Progress,
                    Reward = task.Reward,
                    RewardType = (MissionRewardType)task.RewardType
                });
                i++;
            }

            return result.ToArray();
        }

        public TaskDto AcceptTask(byte type, uint level, byte slot)
        {
            //Logger.Debug($"Claimed mission {TaskLevel}");
            var TaskList = GameServer.Instance.ResourceCache.GetTasks();
            var constants = type == 1;

            var tasks = from t in TaskList
                       where t.Value.constant == constants &&
                       t.Value.Level == level
                       select t.Value;

            if (tasks.Any())
            {
                TaskInfo task = null;
                var rand = new Random();

                while (task == null)
                {
                    foreach (var tt in tasks)
                    {
                        if (tt.GetChance(Player) < rand.Next(100)
                            || _tasksR.ContainsKey(tt.Id))
                            continue;

                        task = tt;

                        break;
                    }
                }

                if (constants)
                    _rMRunning++;
                else
                    _wMRunning++;

                var rt = (task.RewardExp != 0) ? rand.Next(1, 2) : 1;
                var reward = (rt == 1 ? task.RewardPen : task.RewardExp);

                _tasksR.Add(task.Id, new PlayerMissionsDto
                {
                    TaskId = task.Id,
                    PlayerId = Player.Account.Id,
                    Progress = 0,
                    Date = DateTime.Now.ToBinary(),
                    Slot = slot,
                    RewardType = (byte)rt,
                    Reward = (ushort)reward

                });
                Logger.ForAccount(Player)
                    .Debug($"Added Mission {task.Id} - {task.Level} - {task.Name}");
                return new TaskDto
                {
                    Id = task.Id,
                    Unk1 = slot, //Slot?
                    Progress = 0,
                    Reward = reward,
                    RewardType = (MissionRewardType)rt
                };
            }

            return null;
        }

        public ushort UpdateTask(uint Taskid, ushort Progress)
        {
            var TaskInfo = GameServer.Instance.ResourceCache.GetTasks();

            if (_tasksR.ContainsKey(Taskid))
            {
                if (!_taskOnGoing.ContainsKey(Taskid))
                    _taskOnGoing.Add(Taskid, 0);

                var repetition = (ushort)TaskInfo[Taskid].EndCondition.repetetion;

                if (_taskOnGoing[Taskid] < repetition)
                    _taskOnGoing[Taskid]++;
                else
                    return 0xffff;

                return _taskOnGoing[Taskid];

                //if (_taskOnGoing[Taskid] == repetition)
                //{
                //    Player.PEN += task.Reward;
                //    if (task.RewardType == 2)
                //        Player.GainExp(task.Reward);

                //    Player.Session.SendAsync(new SRefreshCashInfoAckMessage { PEN = Player.PEN, AP = Player.AP });
                //}

                //if (task.Progress > repetition)
                //{
                //    task.Progress = repetition;
                //    return 0xffff;
                //}

                //return task.Progress;
            }

            return 0;
        }

        public void Commit(out uint EXP)
        {
            var TaskList = GameServer.Instance.ResourceCache.GetTasks();
            EXP = 0;
            var _needsUpdate = false;

            foreach (var onGoin in _taskOnGoing)
            {
                _tasksR[onGoin.Key].Progress = onGoin.Value;
                if (_tasksR[onGoin.Key].Progress == TaskList[onGoin.Key].EndCondition.repetetion)
                {
                    var rewPEN = TaskList[onGoin.Key].RewardPen;
                    Player.PEN += rewPEN;
                    _needsUpdate = true;
                    Logger
                        .ForAccount(Player.Account)
                        .Information($"Mission {onGoin.Key} Rewards {rewPEN}PEN 0EXP");
                }
            }

            if (_needsUpdate)
            {
                Player
                    .Session
                    .SendAsync(new SRefreshCashInfoAckMessage
                    {
                        PEN = Player.PEN,
                        AP = Player.AP
                    });
            }
        }

        public void OnGoinClear()
        {
            _taskOnGoing.Clear();
            Logger
                .ForAccount(Player.Account)
                .Information("Player leave the room, lose missions progress");
        }

        public void Save(IDbConnection db)
        {
            foreach (var taskKPV in _tasksR)
            {
                var task = taskKPV.Value;
                if (task.Id == 0)
                {
                    db.Insert(task);
                    Logger.Debug($"Mission inserted as {task.Id}");
                }
                else
                {
                    db.Update(task);
                }
            }
        }
    }
}
