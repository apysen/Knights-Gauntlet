"""Geometry-native Gothic skylights, executed by build-royal-hall.py in Blender.

Each window has a pointed frame, two lancets, a rose, leaded mosaic panes,
and a royal crown above a shield. All detail follows the curved vault.
"""
lead=material('Royal Window Lead',(.012,.017,.025))
window_gold=material('Royal Gilt Window Frame',(.68,.43,.13))
azure=material('Stained Glass Azure',(.055,.26,.58))
ivory_glass=material('Stained Glass Pearl',(.54,.65,.67))
violet=material('Stained Glass Amethyst',(.24,.075,.40))

def width_at(v):
    return .88 if v<=.56 else .88*max(0,1-((v-.56)/.44)**1.4)

def point(side,p,layer=0):
    u,v=p;x=side*(2.8+u*.98);y=8.0+v*3.9
    # A recessed planar window keeps every glass/lead layer coplanar. A single
    # non-planar backing polygon otherwise cuts through the curved mosaic.
    slope=-4.4*1.4/5.7*(2.8/5.7)**.4
    z=roof_z(2.8)+slope*(abs(x)-2.8)-.25-layer
    return (x,y,z)

def polygon(side,name,poly,mat,layer=0):
    parts.append(mesh(name,[point(side,p,layer) for p in poly],[tuple(range(len(poly)))],mat))

def stroke(side,name,line,mat=lead,width=.014,closed=False,layer=.025):
    vertices=[];faces=[]
    if closed:line=line+[line[0]]
    for a,b in zip(line,line[1:]):
        dx=b[0]-a[0];dy=(b[1]-a[1])*3.9/.98
        length=math.hypot(dx,dy)
        if length<1e-7:continue
        nx=-dy/length*width/2;ny=dx/length*width/2*.98/3.9
        q=[(a[0]+nx,a[1]+ny),(b[0]+nx,b[1]+ny),(b[0]-nx,b[1]-ny),(a[0]-nx,a[1]-ny)]
        k=len(vertices);vertices.extend(point(side,p,layer) for p in q);faces.append((k,k+1,k+2,k+3))
    if faces:parts.append(mesh(name,vertices,faces,mat))

def circle_line(cx,cy,rx,ry,n=48):
    return [(cx+math.cos(i*math.tau/n)*rx,cy+math.sin(i*math.tau/n)*ry) for i in range(n)]

for side in (-1,1):
    outline=[(-.88,0),(.88,0),(.88,.56)]
    outline += [(width_at(.56+i*.44/24),.56+i*.44/24) for i in range(1,25)]
    outline += [(-width_at(1-i*.44/24),1-i*.44/24) for i in range(1,25)]
    polygon(side,'Pointed lead window backing',outline,lead)
    # Small cut panes form a coherent blue field, amber border and rose pattern.
    # The gaps expose the dark lead beneath, rather than a grid of plain squares.
    columns=14;rows=30
    for row in range(rows):
        v0=row/rows+.002;v1=(row+1)/rows-.002;vm=(v0+v1)/2
        w0=width_at(v0);w1=width_at(v1)
        for col in range(columns):
            a=-1+2*col/columns+.010;b=-1+2*(col+1)/columns-.010
            u=(a+b)/2*width_at(vm)
            poly=[(a*w0,v0),(b*w0,v0),(b*w1,v1),(a*w1,v1)]
            radius=math.hypot(u/.30,(vm-.78)/.125)
            if col in (0,columns-1) or row in (0,1):mat=glass_amber
            elif radius<1:mat=(glass_ruby,azure,glass_amber,violet)[int((math.atan2((vm-.78)/.125,u/.30)+math.pi)/math.tau*12)%4]
            else:mat=azure if (row+col)%5==0 else ivory_glass if row%9==0 and col%3==0 else glass_blue
            polygon(side,'Hand cut jewel glass pane',poly,mat,.012)
    stroke(side,'Carved pointed outer frame',outline,window_gold,.065,True,.035)
    inset=[(u*.94,.018+v*.962) for u,v in outline]
    stroke(side,'Inner lead beading',inset,lead,.022,True,.04)
    # Twin pointed lancets and lower diamond leading.
    for centre in (-.40,.40):
        lancet=[(centre-.29,.07),(centre+.29,.07),(centre+.29,.46),(centre+.20,.56),(centre,.66),(centre-.20,.56),(centre-.29,.46)]
        stroke(side,'Gothic lancet tracery',lancet,window_gold,.022,True,.045)
        for y in (.14,.25,.36):
            diamond=[(centre,y-.05),(centre+.17,y),(centre,y+.05),(centre-.17,y)]
            stroke(side,'Diamond leadwork',diamond,lead,.013,True,.05)
    # A twelve-light rose with quatrefoil petals beneath the pointed arch.
    stroke(side,'Rose window gold rim',circle_line(0,.78,.30,.125),window_gold,.025,True,.045)
    stroke(side,'Rose inner lead ring',circle_line(0,.78,.245,.102),lead,.014,True,.05)
    for i in range(8):
        angle=i*math.tau/8
        cx=math.cos(angle)*.15;cy=.78+math.sin(angle)*.061
        stroke(side,'Rose petal tracery',circle_line(cx,cy,.087,.037,24),lead,.012,True,.055)
        stroke(side,'Rose radial mullion',[(math.cos(angle)*.04,.78+math.sin(angle)*.016),(math.cos(angle)*.29,.78+math.sin(angle)*.12)],window_gold,.012,False,.06)
    polygon(side,'Royal ruby shield',[(-.26,.39),(.26,.39),(.23,.22),(0,.13),(-.23,.22)],glass_ruby,.07)
    stroke(side,'Gilt shield border',[(-.26,.39),(.26,.39),(.23,.22),(0,.13),(-.23,.22)],window_gold,.025,True,.08)
    crown=[(-.32,.43),(-.38,.59),(-.15,.53),(0,.65),(.15,.53),(.38,.59),(.32,.43)]
    polygon(side,'Royal crown in amber glass',crown,glass_amber,.07)
    stroke(side,'Royal crown lead outline',crown,window_gold,.024,True,.08)
    stroke(side,'Crown lower filigree',[(-.32,.43),(.32,.43)],lead,.02,False,.085)
    # A small central fleur-de-lis in the shield gives close views a second motif.
    lily=[(0,.18),(-.04,.26),(-.15,.29),(-.12,.34),(-.04,.31),(0,.37),(.04,.31),(.12,.34),(.15,.29),(.04,.26)]
    polygon(side,'Shield fleur de lis',lily,glass_amber,.09)
