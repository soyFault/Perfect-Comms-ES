import re,subprocess,sys,json,collections
R='.'  # run from the repo root
def git(*a): return subprocess.run(['git','-C',R,*a],capture_output=True,text=True).stdout
lit=re.compile(r'\$?@?"((?:[^"\\]|\\.)*)"')
files=[f for f in git('diff','--name-only','02a8ddc','origin/main').split() if f.endswith('.cs') and 'Tests' not in f]
pairs=[];unpaired=[]
for f in files:
    d=git('diff','-U0','02a8ddc','origin/main','--',f)
    rem=[];add=[]
    def flush():
        global rem,add
        if rem or add:
            r=[l for x in rem for l in lit.findall(x)]; a=[l for x in add for l in lit.findall(x)]
            if len(r)==len(a):
                for x,y in zip(r,a):
                    if x!=y: pairs.append((f,x,y))
            elif r!=a: unpaired.append((f,r,a))
        rem=[];add=[]
    for line in d.splitlines():
        if line.startswith('@@'): flush()
        elif line.startswith('-') and not line.startswith('---'): rem.append(line[1:])
        elif line.startswith('+') and not line.startswith('+++'): add.append(line[1:])
    flush()
ns={f:git('show',f'origin/new-spanish:{f}') for f in files}
out=[]
for f,en,es in pairs:
    out.append(dict(file=f,en=en,es=es,in_new=('"'+en in ns[f]) or (en in ns[f])))
json.dump(dict(pairs=out,unpaired=unpaired),open('pairs.json','w'),ensure_ascii=False,indent=1)
print(len(pairs),'pairs;',sum(p['in_new'] for p in out),'present in new-spanish;',len(unpaired),'unpaired hunks')
c=collections.Counter(p['file'] for p in out);print(c.most_common(8))
