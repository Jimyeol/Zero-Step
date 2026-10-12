"""Validate the stage data currently in the Unity project with the independent Python solver.

usage: python3 -I Tools/StagePipeline/check_project.py [--jobs N] [--quick] [--no-ablation]
Builds a temporary validation folder from Assets/Resources/Stages (+ solutions.txt, Tutorials) and
runs validate.py on it. mapping.csv in this folder holds the reference metrics of the shipped set;
if you change stages, regenerate it (generator/report.py) or run with --quick.
"""
import json, os, shutil, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, '..', '..'))
STAGES = os.path.join(PROJECT, 'Assets', 'Resources', 'Stages')
TUTORIALS = os.path.join(PROJECT, 'Assets', 'Resources', 'Tutorials')

tmp = tempfile.mkdtemp(prefix='zerostep_check_')
try:
    os.makedirs(os.path.join(tmp, 'Stages'))
    for name in os.listdir(STAGES):
        if name.endswith('.json'):
            shutil.copyfile(os.path.join(STAGES, name), os.path.join(tmp, 'Stages', name))
    shutil.copytree(TUTORIALS, os.path.join(tmp, 'Tutorials'), ignore=shutil.ignore_patterns('*.meta', '*.md'))
    shutil.copyfile(os.path.join(HERE, 'mapping.csv'), os.path.join(tmp, 'mapping.csv'))
    solutions = {}
    with open(os.path.join(STAGES, 'solutions.txt'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split('|')
            solutions[parts[0]] = [[[int(v) for v in cell.split(',')] for cell in stroke.split(';')] for stroke in parts[1:]]
    json.dump(solutions, open(os.path.join(tmp, 'solutions.json'), 'w'))
    sys.exit(subprocess.call([sys.executable, '-I', os.path.join(HERE, 'validate.py'), tmp] + sys.argv[1:]))
finally:
    shutil.rmtree(tmp, ignore_errors=True)
