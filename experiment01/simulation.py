import sys
import json
import math
import numpy as np
import argparse

NUM_AGENTS = 50       # エージェント数
NUM_TASKS = 5         # タスク数
SPACE_SIZE = 500.0    # 空間のサイズ (0〜500)
MAX_STEPS = 1000      # 最大シミュレーションステップ
CONVERGENCE_TH = 0.5  # 収束判定閾値（移動速度）
W_A = 1.0             # タスク効率の重み (w_A)
W_B = 5.0             # 倫理的・社会的制約の重み (w_B)

class Task:
    def __init__(self, task_id):
        self.id = task_id
        self.pos = np.random.rand(2) * SPACE_SIZE
        self.req_skill = np.random.rand()

class Agent:
    def __init__(self, agent_id):
        self.id = agent_id
        self.pos = np.random.rand(2) * SPACE_SIZE
        self.skill = np.random.rand()
        self.personality = np.random.rand()
        self.current_target = -1

def calculate_utility(agent, task, all_agents):
    """
    評価関数 (Utility Function): U_i,k = w_A * A(i, k) + w_B * B(i, k)
    """
    dist = np.linalg.norm(agent.pos - task.pos)
    
    skill_match = 1.0 - abs(agent.skill - task.req_skill)
    dist_cost = dist / SPACE_SIZE 
    A = skill_match - dist_cost

    group_agents = [a for a in all_agents if a.current_target == task.id]
    
    B = 0.0
    if len(group_agents) > 0:
        skills = [a.skill for a in group_agents]
        fairness_penalty = np.var(skills) if len(skills) > 1 else 0.0
        
        personalities = [a.personality for a in group_agents]
        diversity = np.var(personalities) if len(personalities) > 1 else 0.0
        personality_penalty = 1.0 - diversity 

        B = - (fairness_penalty + personality_penalty)

    U = (W_A * A) + (W_B * B)
    return U

def main():
    global NUM_AGENTS, NUM_TASKS, W_A, W_B
    
    parser = argparse.ArgumentParser()
    parser.add_argument('--agents', type=int, default=50)
    parser.add_argument('--tasks', type=int, default=5)
    parser.add_argument('--wa', type=float, default=1.0)
    parser.add_argument('--wb', type=float, default=5.0)
    args = parser.parse_args()
    
    NUM_AGENTS = args.agents
    NUM_TASKS = args.tasks
    W_A = args.wa
    W_B = args.wb

    tasks = [Task(i) for i in range(NUM_TASKS)]
    agents = [Agent(i) for i in range(NUM_AGENTS)]

    init_data = {
        "type": "init",
        "tasks": [{"id": t.id, "x": t.pos[0], "y": t.pos[1]} for t in tasks]
    }
    print(json.dumps(init_data))
    sys.stdout.flush()

    for step in range(MAX_STEPS):
        max_movement = 0.0
        
        step_data = {"type": "update", "step": step, "agents": []}

        for agent in agents:
            best_task = None
            max_U = -float('inf')

            for task in tasks:
                U = calculate_utility(agent, task, agents)
                if U > max_U:
                    max_U = U
                    best_task = task

            agent.current_target = best_task.id

            direction = best_task.pos - agent.pos
            dist = np.linalg.norm(direction)
            
            movement_mag = 0.0
            if dist > 1.0:
                speed = min(max(max_U, 0.5), 10.0) 
                velocity = (direction / dist) * speed
                agent.pos += velocity
                movement_mag = np.linalg.norm(velocity)

            if movement_mag > max_movement:
                max_movement = movement_mag

            step_data["agents"].append({
                "id": agent.id,
                "x": agent.pos[0],
                "y": agent.pos[1],
                "target": agent.current_target
            })

        print(json.dumps(step_data))
        sys.stdout.flush()

        if max_movement < CONVERGENCE_TH:
            end_data = {"type": "end", "reason": "converged", "step": step}
            print(json.dumps(end_data))
            sys.stdout.flush()
            break

    else:
        end_data = {"type": "end", "reason": "max_steps", "step": MAX_STEPS}
        print(json.dumps(end_data))
        sys.stdout.flush()

if __name__ == "__main__":
    main()
