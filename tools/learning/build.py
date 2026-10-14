import json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
source=Path(__file__).resolve().parent
rows=(source/'curriculum.txt').read_text().splitlines()
modules=[(1,10,'CMD','Buyruq satridan ishonchli foydalanish','Terminal'),(11,20,'PowerShell','Obyektlar, skriptlar va xavfsiz avtomatlashtirish','Avtomatlashtirish'),(21,25,'Microsoft Word','Tartibli va o‘qilishi oson hujjatlar','Hujjatlar'),(26,30,'Microsoft Excel','Ma’lumot, formulalar va tahlil','Jadvallar'),(31,40,'Flowgorithm','Algoritmlarni ko‘rib va kuzatib o‘rganish','Mantiq'),(41,55,'Vibe Coding · Python','AI yordamida tushunib kod yozish','Dasturlash'),(56,70,'Loyihalar va deploy','G‘oyadan ishlaydigan mahsulotgacha','Amaliyot')]
data={'version':1,'modules':[],'lessons':[]}
for i,(lo,hi,title,desc,tag) in enumerate(modules,1):
 data['modules'].append(dict(id=i,start=lo,end=hi,title=title,description=desc,tag=tag))
for i,row in enumerate(rows,1):
 f=row.split('§');assert len(f)==14,(i,len(f))
 title,body,code,task,criterion,q,a,b,c,q2,a2,b2,c2,why=f
 code=code.replace('\\n','\n').replace('\\u005c','\\')
 if i==16:code='\n'.join(x for x in code.splitlines() if not x.startswith('"$name:'))
 data['lessons'].append(dict(id=i,module=next(m['id'] for m in data['modules'] if m['start']<=i<=m['end']),title=title,body=body,example=code,task=task,criterion=criterion,minutes=60 if 'loyiha' in title.lower() else 45,questions=[dict(id=f'{i}a',prompt=q,options=[a,b,c],correct=a,explain=body),dict(id=f'{i}b',prompt=q2,options=[a2,b2,c2],correct=a2,explain=why)]))
assert len(data['lessons'])==70
(source/'curriculum.json').write_text(json.dumps(data,ensure_ascii=False,indent=2))
original=(source/'math-original.html').read_text()
# Repair existing mathematics without changing its learning-mode feedback.
original=re.sub(r'<link[^>]*fonts[^>]*>','',original)
original=original.replace('text:"−x"},{label:"B",text:"x"},{label:"C",text:"106 − x"','text:"0"},{label:"B",text:"x"},{label:"C",text:"106 − x"')
original=original.replace('const picked = shuffle(BANK).slice(0, TOTAL_Q);','const unique = BANK.filter((q,i,a)=>a.findIndex(x=>x.prompt===q.prompt)===i);\n  const picked = shuffle(unique).slice(0, TOTAL_Q);')
original=original.replace('${exam.group?exam.group+\' · \':\'\'}','${exam.group?esc(exam.group)+\' · \':\'\'}')
original=original.replace('if (exam.answers[qid]) return;','if (!exam || exam.finished || exam.answers[qid]) return;\n  if(Date.now()>=exam.endTime){finishExam(true);return;}')
original=original.replace('function finishExam(auto){\n  clearInterval(timerInt);','function finishExam(auto){\n  if(!exam || exam.finished)return;\n  exam.finished=true;\n  el("finish-modal").classList.remove("show"); closePalette();\n  clearInterval(timerInt);')
original=original.replace('const used = Math.round((Date.now() - exam.startTime) / 1000);','const used = Math.min(TIME_SECONDS,Math.max(0,Math.round((Date.now() - exam.startTime) / 1000)));')
original=original.replace("applyFilter('all');","document.querySelectorAll('.filters button').forEach(b=>b.classList.toggle('on',b.dataset.f==='all'));\n  applyFilter('all');")
start=original.index('  const formData = new FormData();')
end=original.index('\n}\n\nfunction bahoInfo',start)
original=original[:start]+'''  // Explicit user action replaces the previous two automatic POSTs.
  const oldSend=el('send-math-result'); if(oldSend)oldSend.remove();
  const send=document.createElement('button');send.id='send-math-result';send.className='btn btn-ghost';
  send.textContent='Natijani markazga yuborish';
  send.onclick=async()=>{
    if(!confirm('Ism, familiya, guruh va javoblar markazning Google Apps Script manziliga yuboriladi. Davom etasizmi?'))return;
    send.disabled=true;send.textContent='Yuborilmoqda…';
    const formData=new FormData();for(const [key,value] of Object.entries(payload))formData.append(key,value);
    try{const response=await fetch('https://script.google.com/macros/s/AKfycbxjgC415jKzU6yXT8DlZlBKEQrzVEFumdBgXsB0NDmwCKWD0dmk1J42koZKcElI6QEV/exec',{method:'POST',body:formData});
      send.textContent=response.ok?'Server so‘rovni qabul qildi; yozuvni ustoz tekshiradi.':'Server xatosi. Yozuvni ustoz tekshirsin.';
    }catch{send.textContent='Yetib borgani noma’lum. Takror yuborishdan oldin ustoz tekshirsin.';}
  };
  el('print-btn').parentElement.appendChild(send);
''' +original[end:]
original=original.replace("if(!screens.exam.classList.contains('active'))return;","if(!screens.exam.classList.contains('active') || document.getElementById('math-app').hidden || el('finish-modal').classList.contains('show'))return;")
original=original.replace('10 ta savol · 30 daqiqa.','10 ta savol · 30 daqiqa. Mashq rejimi: har tanlovdan keyin izoh ko‘rsatiladi; javob qayta tanlanmaydi.')
original=original.replace('<title>Mirae · DTM Majburiy Matematika — Ifodalarni shakl almashtirish</title>','<title>Mirae · To‘garak va matematika</title>')
# Preserve existing screens/scripts inside an isolated wrapper.
original=original.replace('<body>','<body>\n'+(source/'club.html').read_text()+'\n<div id="math-app" hidden>')
original=original.replace('</body>','</div>\n<style>'+(source/'club.css').read_text()+'</style>\n<script id="curriculum" type="application/json">'+json.dumps(data,ensure_ascii=False).replace('<','\\u003c')+'</script>\n<script>'+(source/'club.js').read_text()+'</script>\n</body>')
original=original.replace('<html lang="uz">','<html lang="uz">')
output=root/'src/ClassroomGuard.StudentShell/Learning/index.html'
output.write_text(original)
(root/'Mirae-Togarak.html').write_text(original)
print('Built',len(data['lessons']),'lessons,',sum(len(l['questions']) for l in data['lessons']),'questions;',len(original),'characters')
