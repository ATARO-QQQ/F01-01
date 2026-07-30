import sys
import json
import math
import random
import numpy as np

def U_i(s_i, r_j):
    """
    エージェント a_i がグループ g_j に所属した場合の効用
    数式: U_i(g_j) = - || s_i - r_j ||_2
    """
    return -np.linalg.norm(s_i - r_j)

def U_total(agents_skills, groups_reqs, allocation):
    """
    大域的総効用 (Global Utility)
    数式: U_total = Σ_j Σ_{i ∈ g_j} U_i(g_j)
    """
    total = 0.0
    for i in range(len(agents_skills)):
        total += U_i(agents_skills[i], groups_reqs[allocation[i]])
    return total

def run_simulation(N, M, K):
    np.random.seed()
    
    agents_skills = np.random.rand(N, K)
    
    groups_reqs = np.random.rand(M, K)
    
    C_j = (N // M) + 1 
  
    mas_allocation = np.zeros(N, dtype=int)
    group_counts = np.zeros(M, dtype=int)
    for i in range(N):
        available_groups = np.where(group_counts < C_j)[0]
        if len(available_groups) > 0:
            g = np.random.choice(available_groups)
        else:
            g = np.argmin(group_counts)
        mas_allocation[i] = g
        group_counts[g] += 1
        
    mas_history = []
    converged = False
    step = 0
    max_steps = 10000
    
    while not converged and step < max_steps:
        converged = True 
        
        agent_indices = np.arange(N)
        np.random.shuffle(agent_indices)
        
        for i in agent_indices:
            current_g = mas_allocation[i]
            best_g = current_g
            best_utility = U_i(agents_skills[i], groups_reqs[current_g])
            
            group_counts[current_g] -= 1
            
            for g in range(M):
                if group_counts[g] < C_j:
                    u = U_i(agents_skills[i], groups_reqs[g])
                    if u > best_utility:
                        best_utility = u
                        best_g = g
            
            if best_g != current_g:
                mas_allocation[i] = best_g
                group_counts[best_g] += 1
                converged = False
            else:
                group_counts[current_g] += 1
                
        mas_history.append(U_total(agents_skills, groups_reqs, mas_allocation))
        step += 1

    mas_final_utility = mas_history[-1] if mas_history else 0

    sa_allocation = np.copy(mas_allocation)
    np.random.shuffle(sa_allocation)
    sa_group_counts = np.zeros(M, dtype=int)
    for g in sa_allocation:
        sa_group_counts[g] += 1
        
    current_sa_utility = U_total(agents_skills, groups_reqs, sa_allocation)
    best_sa_utility = current_sa_utility
    sa_history = [current_sa_utility]
    
    T = 10.0      # 初期温度
    T_min = 0.001 # 終了温度
    alpha = 0.99  # 冷却率 
    sa_step = 0
    
    while T > T_min and sa_step < max_steps:
        if random.random() < 0.5:
            i1 = random.randint(0, N - 1)
            g_old = sa_allocation[i1]
            available_groups = np.where(sa_group_counts < C_j)[0]
            if len(available_groups) > 0:
                g_new = random.choice(available_groups)
                if g_old != g_new:
                    diff = U_i(agents_skills[i1], groups_reqs[g_new]) - U_i(agents_skills[i1], groups_reqs[g_old])
                    if diff > 0 or math.exp(diff / T) > random.random():
                        sa_allocation[i1] = g_new
                        sa_group_counts[g_old] -= 1
                        sa_group_counts[g_new] += 1
                        current_sa_utility += diff
        else:
            i1, i2 = random.sample(range(N), 2)
            g1, g2 = sa_allocation[i1], sa_allocation[i2]
            if g1 != g2:
                u_old = U_i(agents_skills[i1], groups_reqs[g1]) + U_i(agents_skills[i2], groups_reqs[g2])
                u_new = U_i(agents_skills[i1], groups_reqs[g2]) + U_i(agents_skills[i2], groups_reqs[g1])
                diff = u_new - u_old
                if diff > 0 or math.exp(diff / T) > random.random():
                    sa_allocation[i1], sa_allocation[i2] = g2, g1
                    current_sa_utility += diff
        
        if current_sa_utility > best_sa_utility:
            best_sa_utility = current_sa_utility
            
        sa_history.append(current_sa_utility)
        T *= alpha
        sa_step += 1

    result = {
        "N": N,
        "M": M,
        "K": K,
        "Capacity_C_j": int(C_j),
        "MAS_Steps": step,
        "MAS_FinalUtility": mas_final_utility,
        "SA_BestUtility": best_sa_utility,
        "ApproximationRatio": mas_final_utility / best_sa_utility if best_sa_utility != 0 else 0,
        "MAS_History": mas_history,
        "SA_History": sa_history
    }
    
    print(json.dumps(result))

if __name__ == "__main__":
    try:
        n_agents = int(sys.argv[1])
        m_groups = int(sys.argv[2])
        k_skills = int(sys.argv[3])
    except IndexError:
        n_agents, m_groups, k_skills = 200, 20, 3 
        
    run_simulation(n_agents, m_groups, k_skills)
