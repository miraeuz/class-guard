'use strict';
const allowed=new Set(['P','B','STRONG','I','EM','U','BR','UL','OL','LI','PRE','CODE','SUB','SUP','TABLE','THEAD','TBODY','TR','TH','TD','BLOCKQUOTE','SPAN']);
const drop=new Set(['SCRIPT','STYLE','IFRAME','OBJECT','EMBED','SVG','MATH','LINK','META','FORM','INPUT','BUTTON','IMG','VIDEO','AUDIO','TEMPLATE']);
function cleanHtml(input){
 const parsed=new DOMParser().parseFromString(input,'text/html');
 function clean(n){
  if(n.nodeType===Node.TEXT_NODE)return document.createTextNode(n.textContent);
  if(n.nodeType!==Node.ELEMENT_NODE||drop.has(n.tagName))return document.createDocumentFragment();
  const out=allowed.has(n.tagName)?document.createElement(n.tagName.toLowerCase()):document.createDocumentFragment();
  for(const child of n.childNodes)out.append(clean(child));
  return out;
 }
 const out=document.createDocumentFragment();for(const n of parsed.body.childNodes)out.append(clean(n));return out;
}
function paintCode(text,language){
 const target=document.getElementById('code');target.replaceChildren();
 if(!['python','py','javascript','js','powershell'].includes(language.toLowerCase())){target.textContent=text;return;}
 const pattern=/(#[^\n]*|\/\/[^\n]*|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\b(?:def|return|if|else|elif|for|while|in|import|from|class|try|except|True|False|None|const|let|function|print|input)\b|\b\d+(?:\.\d+)?\b)/g;
 let end=0;for(const match of text.matchAll(pattern)){target.append(document.createTextNode(text.slice(end,match.index)));const span=document.createElement('span');span.textContent=match[0];span.className=/^(#|\/\/)/.test(match[0])?'tok-comment':/^['"]/.test(match[0])?'tok-string':/^\d/.test(match[0])?'tok-number':'tok-key';target.append(span);end=match.index+match[0].length;}target.append(document.createTextNode(text.slice(end)));
}
function renderQuestion(q){
 document.documentElement.classList.toggle("dark",q.Dark===true);
 const prompt=document.getElementById('prompt');prompt.replaceChildren();
 if(q.TextHtml)prompt.append(cleanHtml(String(q.TextHtml).slice(0,12000)));else prompt.textContent=q.Text||'';
 document.getElementById('meta').textContent=[q.Topic,q.Difficulty].filter(Boolean).join(' · ');
 const code=String(q.Code||'').slice(0,12000);document.getElementById('codebox').hidden=!code;document.getElementById('language').textContent=q.Language||'CODE';paintCode(code,String(q.Language||''));
 const math=document.getElementById('math');math.replaceChildren();math.hidden=!q.Latex;document.getElementById('math-error').textContent='';
 if(q.Latex){try{katex.render(String(q.Latex).slice(0,4000),math,{displayMode:true,throwOnError:true,trust:false,strict:'error',maxExpand:100,maxSize:10});}catch{math.textContent=q.Latex;document.getElementById('math-error').textContent='Formula matn ko‘rinishida. LaTeX yozilishini tekshiring.';}}
 notifySize();
}
function notifySize(){if(window.chrome?.webview)window.chrome.webview.postMessage({height:Math.min(520,Math.max(100,document.getElementById('question').scrollHeight+24))});}
if(window.chrome?.webview)window.chrome.webview.addEventListener('message',e=>renderQuestion(e.data));
new ResizeObserver(notifySize).observe(document.getElementById('question'));
