// Histórico público de Releases: versões estáveis e pré-releases Internal.
const REPO="dieguesmosken/dead-frequency",API="https://api.github.com/repos/dieguesmosken/dead-frequency";
const $=s=>document.querySelector(s);
const esc=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const date=s=>s?new Date(s).toLocaleDateString("pt-BR"):"Data não informada";
const privateRelease=r=>/SITE_VISIBILITY\s*:\s*PRIVATE/i.test(r.body||"")||/\[SITE_PRIVATE\]/i.test(r.body||"");
const isInternal=r=>/^internal-v/i.test(r.tag_name||"")||/internal/i.test(r.name||"");
let releases=[],selected="stable";
function safeMarkdown(text){
 const lines=String(text||"").split(/\r?\n/);let out="",inList=false;
 const inline=s=>esc(s).replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g,'<a href="$2" target="_blank" rel="noopener">$1 ↗</a>').replace(/\*\*(.*?)\*\*/g,"<strong>$1</strong>").replace(/\x60([^\x60]+)\x60/g,"<code>$1</code>");
 for(const line of lines){const t=line.trim();if(/^[-*] /.test(t)){if(!inList){out+="<ul>";inList=true;}out+="<li>"+inline(t.slice(2))+"</li>";continue;}if(inList){out+="</ul>";inList=false;}if(!t)continue;
 if(/^#{1,4} /.test(t)){const n=Math.min(4,(t.match(/^#+/)[0].length));out+="<h"+n+">"+inline(t.replace(/^#{1,4} /,""))+"</h"+n+">";}
 else if(/^---+$/.test(t))out+="<hr>";else if(!/^<img\b/i.test(t))out+="<p>"+inline(t)+"</p>";}
 if(inList)out+="</ul>";return out;
}
function card(r,list,index){
 const internal=isInternal(r),previous=list[index+1],kind=internal?"Internal · Pré-alpha":"Versão pública";
 const compare=previous?"https://github.com/"+REPO+"/compare/"+encodeURIComponent(previous.tag_name)+"..."+encodeURIComponent(r.tag_name):"https://github.com/"+REPO+"/tree/"+encodeURIComponent(r.target_commitish||"main");
 const assets=(r.assets||[]).filter(a=>a.name),downloads=assets.map(a=>'<a href="'+esc(a.browser_download_url)+'" target="_blank" rel="noopener">'+esc(a.name)+" ("+(a.size/1048576).toFixed(1)+" MB)</a>").join("");
 const notes=(r.body||"").replace(/<!--\s*SITE_VISIBILITY\s*:\s*PRIVATE\s*-->/ig,"").replace(/\[SITE_PRIVATE\]/ig,"").trim();
 return '<details class="release" '+(index===0?"open":"")+'><summary><span class="ver">'+esc(r.name||r.tag_name)+'</span><span class="title">'+esc(r.tag_name)+'</span><span class="date">'+esc(date(r.published_at||r.created_at))+'</span></summary><div class="body"><div class="release-meta"><span class="release-kind '+(internal?"internal":"")+'">'+kind+'</span></div><div class="release-body">'+(notes?safeMarkdown(notes):'<p class="dim">Esta publicação não possui notas detalhadas.</p>')+'</div><div class="release-links"><a href="'+esc(r.html_url)+'" target="_blank" rel="noopener">Release no GitHub ↗</a><a href="'+compare+'" target="_blank" rel="noopener">Commits e arquivos desta versão ↗</a>'+downloads+'</div><button class="btn small-btn ghost commits-toggle" type="button" data-tag="'+esc(r.tag_name)+'" data-base="'+esc(previous?.tag_name||"")+'">Ver commits individuais</button><div class="commit-list" hidden></div></div></details>';
}
function render(){
 const list=releases.filter(r=>!privateRelease(r)&&(selected==="internal"?isInternal(r):!isInternal(r)&&!r.prerelease));
 $("#release-list").hidden=selected==="roadmap";$("#roadmap").hidden=selected!=="roadmap";$("#release-status").hidden=selected==="roadmap";
 if(selected==="roadmap")return;
 $("#release-status").textContent=list.length?list.length+" atualização(ões) · histórico sincronizado com GitHub":"Nenhuma atualização pública encontrada nesta categoria.";
 $("#release-list").innerHTML=list.length?list.map((r,i)=>card(r,list,i)).join(""):'<div class="updates-empty">Ainda não há versões publicadas nesta categoria.</div>';
 $("#release-list").querySelectorAll(".commits-toggle").forEach(btn=>btn.addEventListener("click",()=>loadCommits(btn)));
}
async function loadCommits(btn){
 const panel=btn.nextElementSibling;if(!panel.hidden){panel.hidden=true;btn.textContent="Ver commits individuais";return;}
 panel.hidden=false;panel.innerHTML="<span class='dim'>Buscando commits…</span>";
 try{
  const tag=btn.dataset.tag,base=btn.dataset.base,url=base?API+"/compare/"+encodeURIComponent(base)+"..."+encodeURIComponent(tag):API+"/commits/"+encodeURIComponent(tag);
  const res=await fetch(url,{headers:{"Accept":"application/vnd.github+json"}});if(!res.ok)throw new Error("GitHub não retornou o comparativo");
  const data=await res.json(),commits=base?(data.commits||[]):[{sha:data.sha,commit:data.commit,html_url:data.html_url}];
  if(!commits.length){panel.innerHTML="<span class='dim'>Nenhum commit novo encontrado entre essas versões.</span>";return;}
  panel.innerHTML="<b>Commits incluídos</b><ul>"+commits.map(c=>'<li><a href="'+esc(c.html_url||("https://github.com/"+REPO+"/commit/"+c.sha))+'" target="_blank" rel="noopener"><code>'+esc(c.sha.slice(0,7))+"</code> "+esc(c.commit?.message?.split("\n")[0]||"Commit")+"</a></li>").join("")+"</ul>";
  btn.textContent="Ocultar "+commits.length+" commit(s)";
 }catch(e){panel.innerHTML="<span class='dim'>"+esc(e.message)+'. <a href="https://github.com/'+REPO+'/commits/main/" target="_blank" rel="noopener">Abrir histórico no GitHub ↗</a></span>';}
}
async function loadRoadmap(){
 const el=$("#roadmap-content");try{const res=await fetch("https://raw.githubusercontent.com/"+REPO+"/main/ROADMAP.md",{cache:"no-cache"});if(!res.ok)throw new Error("Não foi possível buscar o ROADMAP.md");el.innerHTML=safeMarkdown(await res.text());}
 catch(e){el.innerHTML='<p class="dim">'+esc(e.message)+'. Consulte o <a href="https://github.com/'+REPO+'/blob/main/ROADMAP.md">arquivo no GitHub</a>.</p>';}
}
document.querySelectorAll(".update-tab").forEach(tab=>tab.addEventListener("click",()=>{
 selected=tab.dataset.tab;document.querySelectorAll(".update-tab").forEach(t=>{const active=t===tab;t.classList.toggle("active",active);t.setAttribute("aria-selected",String(active));});
 if(selected==="roadmap"&&!$("#roadmap-content").dataset.loaded){$("#roadmap-content").dataset.loaded="1";loadRoadmap();}
 render();if(location.hash!=="#"+selected)history.replaceState(null,"","#"+selected);
}));
// Topo: última versão publicada (site.json primeiro; depois a última release que não é pré-release).
function setBrand(v){if(v)document.querySelectorAll(".brand-version").forEach(e=>e.textContent="v"+String(v).replace(/^v/,""));}
fetch("data/site.json",{cache:"no-cache"}).then(r=>r.json()).then(d=>setBrand(d.version)).catch(()=>{});
async function init(){
 try{
  const res=await fetch(API+"/releases?per_page=100",{headers:{"Accept":"application/vnd.github+json"},cache:"no-cache"});if(!res.ok)throw new Error("A API do GitHub está indisponível ou atingiu o limite de consultas.");
  releases=(await res.json()).filter(r=>!r.draft).sort((a,b)=>new Date(b.published_at||b.created_at)-new Date(a.published_at||a.created_at));
  const stable=releases.find(r=>!r.prerelease);if(stable)setBrand(stable.tag_name);
  if(location.hash==="#internal")selected="internal";if(location.hash==="#roadmap"){selected="roadmap";$("#roadmap-content").dataset.loaded="1";loadRoadmap();}
  const tab=document.querySelector('.update-tab[data-tab="'+selected+'"]');if(tab)document.querySelectorAll(".update-tab").forEach(t=>{const active=t===tab;t.classList.toggle("active",active);t.setAttribute("aria-selected",String(active));});
  render();
 }catch(e){$("#release-status").textContent=e.message;$("#release-list").innerHTML='<div class="updates-empty">O histórico continua disponível em <a href="https://github.com/'+REPO+'/releases">GitHub Releases ↗</a>.</div>';}
}
window.addEventListener("hashchange",()=>{if(location.hash==="#roadmap"){document.querySelector(".update-tab[data-tab=roadmap]").click();}else if(location.hash==="#internal"){document.querySelector(".update-tab[data-tab=internal]").click();}});
init();
