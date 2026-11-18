using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace FortniteObj;
// Realize only visible card rows, keeping large game catalogues responsive.
public sealed class VirtualizingCardPanel : VirtualizingPanel, IScrollInfo {
 public static readonly DependencyProperty ItemWidthProperty=DependencyProperty.Register(nameof(ItemWidth),typeof(double),typeof(VirtualizingCardPanel),new FrameworkPropertyMetadata(190d,FrameworkPropertyMetadataOptions.AffectsMeasure));
 public static readonly DependencyProperty ItemHeightProperty=DependencyProperty.Register(nameof(ItemHeight),typeof(double),typeof(VirtualizingCardPanel),new FrameworkPropertyMetadata(248d,FrameworkPropertyMetadataOptions.AffectsMeasure));
 public double ItemWidth{get=>(double)GetValue(ItemWidthProperty);set=>SetValue(ItemWidthProperty,value);}
 public double ItemHeight{get=>(double)GetValue(ItemHeightProperty);set=>SetValue(ItemHeightProperty,value);}
 static readonly DependencyProperty IndexProperty=DependencyProperty.RegisterAttached("CardIndex",typeof(int),typeof(VirtualizingCardPanel),new PropertyMetadata(-1));
 double width,height,extent,offset;int columns=1;bool measuring;
 protected override void OnItemsChanged(object sender,ItemsChangedEventArgs args){base.OnItemsChanged(sender,args);if(args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset){if(InternalChildren.Count>0)RemoveInternalChildRange(0,InternalChildren.Count);offset=0;}else if(args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or System.Collections.Specialized.NotifyCollectionChangedAction.Replace or System.Collections.Specialized.NotifyCollectionChangedAction.Move){int first=Math.Max(0,args.Position.Index);int count=Math.Min(args.ItemUICount,InternalChildren.Count-first);if(count>0)RemoveInternalChildRange(first,count);}InvalidateMeasure();}
 protected override Size MeasureOverride(Size available){
  measuring=true;
  try{
   width=double.IsInfinity(available.Width)?Math.Max(190,ActualWidth):Math.Max(1,available.Width);height=double.IsInfinity(available.Height)?Math.Max(248,ActualHeight):Math.Max(1,available.Height);
   columns=Math.Max(1,(int)(width/Math.Max(1,ItemWidth)));int count=ItemsControl.GetItemsOwner(this)?.Items.Count??0;extent=Math.Ceiling((double)count/columns)*ItemHeight;offset=Math.Clamp(offset,0,Math.Max(0,extent-height));ScrollOwner?.InvalidateScrollInfo();
   int first=Math.Max(0,((int)(offset/ItemHeight)-1)*columns),last=Math.Min(count-1,((int)((offset+height)/ItemHeight)+1)*columns+columns-1);
   var generator=ItemContainerGenerator;var owner=ItemsControl.GetItemsOwner(this);
   for(int i=InternalChildren.Count-1;i>=0;i--){var child=InternalChildren[i];int index=owner.ItemContainerGenerator.IndexFromContainer(child);if(index<first||index>last){var position=generator.GeneratorPositionFromIndex(index);if(index>=0&&position.Offset==0&&position.Index>=0){if(generator is IRecyclingItemContainerGenerator recycler)recycler.Recycle(position,1);else generator.Remove(position,1);}RemoveInternalChildRange(i,1);}}
   if(count>0){var start=generator.GeneratorPositionFromIndex(first);int childIndex=0;using(generator.StartAt(start,GeneratorDirection.Forward,true)){for(int index=first;index<=last;index++,childIndex++){
    bool isNew;var child=(UIElement)generator.GenerateNext(out isNew);
    // ListBox can realize a selected item before the panel does; add it even when the generator reports it as existing.
    int existing=InternalChildren.IndexOf(child);if(existing<0){if(childIndex>=InternalChildren.Count)AddInternalChild(child);else InsertInternalChild(childIndex,child);generator.PrepareItemContainer(child);}else if(existing!=childIndex){RemoveInternalChildRange(existing,1);InsertInternalChild(Math.Min(childIndex,InternalChildren.Count),child);}
    child.SetValue(IndexProperty,index);child.Measure(new Size(width/columns,ItemHeight));
   }}}
   return new Size(width,height);
  }finally{measuring=false;}
 }
 protected override Size ArrangeOverride(Size size){double cell=size.Width/columns;foreach(UIElement child in InternalChildren){int i=(int)child.GetValue(IndexProperty);child.Arrange(new Rect((i%columns)*cell,(i/columns)*ItemHeight-offset,cell,ItemHeight));}return size;}
 protected override void BringIndexIntoView(int index){double top=(index/columns)*ItemHeight;if(top<offset)SetVerticalOffset(top);else if(top+ItemHeight>offset+height)SetVerticalOffset(top+ItemHeight-height);}
 public bool CanHorizontallyScroll{get;set;}public bool CanVerticallyScroll{get;set;}=true;public double ExtentWidth=>width;public double ExtentHeight=>extent;public double ViewportWidth=>width;public double ViewportHeight=>height;public double HorizontalOffset=>0;public double VerticalOffset=>offset;public ScrollViewer? ScrollOwner{get;set;}
 public void SetHorizontalOffset(double value){}public void SetVerticalOffset(double value){var next=Math.Clamp(double.IsNaN(value)?0:value,0,Math.Max(0,extent-height));if(Math.Abs(next-offset)<.1)return;offset=next;if(!measuring)InvalidateMeasure();ScrollOwner?.InvalidateScrollInfo();}
 public void LineUp()=>SetVerticalOffset(offset-48);public void LineDown()=>SetVerticalOffset(offset+48);public void PageUp()=>SetVerticalOffset(offset-height);public void PageDown()=>SetVerticalOffset(offset+height);public void MouseWheelUp()=>SetVerticalOffset(offset-144);public void MouseWheelDown()=>SetVerticalOffset(offset+144);public void LineLeft(){}public void LineRight(){}public void PageLeft(){}public void PageRight(){}public void MouseWheelLeft(){}public void MouseWheelRight(){}
 public Rect MakeVisible(Visual visual,Rect rectangle){
  if(!IsAncestorOf(visual)&&visual!=this)return Rect.Empty;
  var bounds=visual.TransformToAncestor(this).TransformBounds(rectangle);double top=offset+bounds.Top;
  if(top<offset)SetVerticalOffset(top);else if(top+bounds.Height>offset+height)SetVerticalOffset(top+bounds.Height-height);
  var result=new Rect(bounds.X,top-offset,bounds.Width,bounds.Height);result.Intersect(new Rect(0,0,width,height));return result;
 }
}
