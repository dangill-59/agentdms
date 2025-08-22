/**
 * AgentDMS Annotation Component
 * Handles document annotation using Fabric.js
 */
class AgentDMSAnnotator {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.options = {
            enableDrawing: true,
            enableHighlighting: true,
            enableRedaction: true,
            enableText: true,
            strokeWidth: 2,
            highlightOpacity: 0.3,
            defaultStrokeColor: '#ff0000',
            defaultHighlightColor: '#ffff00',
            defaultRedactionColor: '#000000',
            ...options
        };
        
        this.canvas = null;
        this.isAnnotationMode = false;
        this.currentTool = 'none';
        this.isDrawing = false;
        this.originalImageData = null;
        
        this.tools = {
            DRAW: 'draw',
            HIGHLIGHT: 'highlight',
            REDACT: 'redact',
            TEXT: 'text',
            NONE: 'none'
        };
    }

    initialize() {
        if (!this.canvas) {
            this.createCanvas();
        }
        this.setupEventListeners();
    }

    createCanvas() {
        const container = document.getElementById(this.containerId);
        if (!container) {
            throw new Error(`Container with ID '${this.containerId}' not found`);
        }

        // Create canvas element
        const canvasElement = document.createElement('canvas');
        canvasElement.id = 'annotationCanvas';
        canvasElement.style.position = 'absolute';
        canvasElement.style.top = '0';
        canvasElement.style.left = '0';
        canvasElement.style.pointerEvents = 'none';
        canvasElement.style.zIndex = '10';
        
        container.appendChild(canvasElement);

        // Initialize Fabric.js canvas
        this.canvas = new fabric.Canvas('annotationCanvas', {
            isDrawingMode: false,
            selection: true,
            preserveObjectStacking: true
        });

        this.resizeCanvas();
    }

    resizeCanvas() {
        if (!this.canvas) return;

        const container = document.getElementById(this.containerId);
        const rect = container.getBoundingClientRect();
        
        this.canvas.setDimensions({
            width: rect.width,
            height: rect.height
        });
    }

    setupEventListeners() {
        // Resize canvas when container resizes
        window.addEventListener('resize', () => {
            this.resizeCanvas();
        });

        // Tool-specific event handlers
        this.canvas.on('mouse:down', (e) => {
            this.handleMouseDown(e);
        });

        this.canvas.on('mouse:move', (e) => {
            this.handleMouseMove(e);
        });

        this.canvas.on('mouse:up', (e) => {
            this.handleMouseUp(e);
        });

        this.canvas.on('path:created', (e) => {
            this.handlePathCreated(e);
        });
    }

    enableAnnotation() {
        if (!this.canvas) {
            this.initialize();
        }
        
        this.isAnnotationMode = true;
        this.canvas.getElement().style.pointerEvents = 'all';
        
        // Show annotation toolbar
        this.showAnnotationTools();
        
        // Store original image data
        this.storeOriginalImageData();
    }

    disableAnnotation() {
        this.isAnnotationMode = false;
        this.currentTool = this.tools.NONE;
        this.canvas.isDrawingMode = false;
        this.canvas.getElement().style.pointerEvents = 'none';
        
        // Hide annotation toolbar
        this.hideAnnotationTools();
    }

    setTool(tool) {
        if (!this.isAnnotationMode) return;
        
        this.currentTool = tool;
        this.canvas.isDrawingMode = false;
        this.canvas.selection = true;
        
        switch (tool) {
            case this.tools.DRAW:
                this.setupDrawingTool();
                break;
            case this.tools.HIGHLIGHT:
                this.setupHighlightTool();
                break;
            case this.tools.REDACT:
                this.setupRedactionTool();
                break;
            case this.tools.TEXT:
                this.setupTextTool();
                break;
            default:
                this.canvas.defaultCursor = 'default';
                break;
        }
        
        this.updateToolUI();
    }

    setupDrawingTool() {
        this.canvas.isDrawingMode = true;
        this.canvas.freeDrawingBrush.width = this.options.strokeWidth;
        this.canvas.freeDrawingBrush.color = this.options.defaultStrokeColor;
        this.canvas.defaultCursor = 'crosshair';
    }

    setupHighlightTool() {
        this.canvas.isDrawingMode = true;
        this.canvas.freeDrawingBrush.width = 10;
        this.canvas.freeDrawingBrush.color = this.options.defaultHighlightColor;
        this.canvas.defaultCursor = 'crosshair';
        
        // Set opacity for highlighting effect
        this.canvas.freeDrawingBrush.globalCompositeOperation = 'multiply';
    }

    setupRedactionTool() {
        this.canvas.isDrawingMode = false;
        this.canvas.selection = false;
        this.canvas.defaultCursor = 'crosshair';
    }

    setupTextTool() {
        this.canvas.isDrawingMode = false;
        this.canvas.selection = true;
        this.canvas.defaultCursor = 'text';
    }

    handleMouseDown(e) {
        if (!this.isAnnotationMode) return;
        
        const pointer = this.canvas.getPointer(e.e);
        
        switch (this.currentTool) {
            case this.tools.REDACT:
                this.startRedaction(pointer);
                break;
            case this.tools.TEXT:
                this.addText(pointer);
                break;
        }
    }

    handleMouseMove(e) {
        if (!this.isAnnotationMode || !this.isDrawing) return;
        
        const pointer = this.canvas.getPointer(e.e);
        
        if (this.currentTool === this.tools.REDACT && this.activeRedaction) {
            this.updateRedaction(pointer);
        }
    }

    handleMouseUp(e) {
        if (!this.isAnnotationMode) return;
        
        if (this.currentTool === this.tools.REDACT && this.activeRedaction) {
            this.finishRedaction();
        }
        
        this.isDrawing = false;
    }

    handlePathCreated(e) {
        if (this.currentTool === this.tools.HIGHLIGHT) {
            // Apply highlighting effect
            e.path.set({
                opacity: this.options.highlightOpacity,
                globalCompositeOperation: 'multiply'
            });
            this.canvas.renderAll();
        }
    }

    startRedaction(pointer) {
        this.isDrawing = true;
        this.redactionStart = pointer;
        
        this.activeRedaction = new fabric.Rect({
            left: pointer.x,
            top: pointer.y,
            width: 0,
            height: 0,
            fill: this.options.defaultRedactionColor,
            selectable: true,
            evented: true
        });
        
        this.canvas.add(this.activeRedaction);
    }

    updateRedaction(pointer) {
        if (!this.activeRedaction) return;
        
        const width = pointer.x - this.redactionStart.x;
        const height = pointer.y - this.redactionStart.y;
        
        this.activeRedaction.set({
            width: Math.abs(width),
            height: Math.abs(height),
            left: width > 0 ? this.redactionStart.x : pointer.x,
            top: height > 0 ? this.redactionStart.y : pointer.y
        });
        
        this.canvas.renderAll();
    }

    finishRedaction() {
        this.activeRedaction = null;
        this.redactionStart = null;
    }

    addText(pointer) {
        const text = prompt('Enter text:');
        if (text) {
            const textObject = new fabric.Text(text, {
                left: pointer.x,
                top: pointer.y,
                fontSize: 16,
                fill: '#000000',
                selectable: true,
                editable: true
            });
            
            this.canvas.add(textObject);
            this.canvas.setActiveObject(textObject);
        }
    }

    clearAnnotations() {
        if (!this.canvas) return;
        
        const objects = this.canvas.getObjects();
        objects.forEach(obj => {
            if (obj.type !== 'image') {
                this.canvas.remove(obj);
            }
        });
        
        this.canvas.renderAll();
    }

    getAnnotations() {
        if (!this.canvas) return [];
        
        return this.canvas.getObjects().filter(obj => obj.type !== 'image').map(obj => ({
            type: obj.type,
            data: obj.toObject()
        }));
    }

    loadAnnotations(annotations) {
        if (!this.canvas || !annotations) return;
        
        annotations.forEach(annotation => {
            let obj;
            switch (annotation.type) {
                case 'path':
                    obj = new fabric.Path(annotation.data.path, annotation.data);
                    break;
                case 'rect':
                    obj = new fabric.Rect(annotation.data);
                    break;
                case 'text':
                    obj = new fabric.Text(annotation.data.text, annotation.data);
                    break;
                default:
                    return;
            }
            
            if (obj) {
                this.canvas.add(obj);
            }
        });
        
        this.canvas.renderAll();
    }

    exportAnnotatedImage() {
        if (!this.canvas) return null;
        
        return this.canvas.toDataURL({
            format: 'png',
            quality: 1.0,
            multiplier: 2
        });
    }

    storeOriginalImageData() {
        const img = document.querySelector('.document-image');
        if (img) {
            this.originalImageData = {
                src: img.src,
                width: img.naturalWidth,
                height: img.naturalHeight
            };
        }
    }

    showAnnotationTools() {
        const toolsContainer = document.getElementById('annotationTools');
        if (toolsContainer) {
            toolsContainer.style.display = 'block';
        }
    }

    hideAnnotationTools() {
        const toolsContainer = document.getElementById('annotationTools');
        if (toolsContainer) {
            toolsContainer.style.display = 'none';
        }
    }

    updateToolUI() {
        // Update button states to show active tool
        const buttons = {
            'drawBtn': this.tools.DRAW,
            'highlightBtn': this.tools.HIGHLIGHT,
            'redactBtn': this.tools.REDACT
        };
        
        Object.keys(buttons).forEach(buttonId => {
            const button = document.getElementById(buttonId);
            if (button) {
                if (buttons[buttonId] === this.currentTool) {
                    button.classList.add('active');
                } else {
                    button.classList.remove('active');
                }
            }
        });
    }

    // Public API methods
    toggle() {
        if (this.isAnnotationMode) {
            this.disableAnnotation();
        } else {
            this.enableAnnotation();
        }
    }

    isActive() {
        return this.isAnnotationMode;
    }

    getCurrentTool() {
        return this.currentTool;
    }

    setStrokeColor(color) {
        this.options.defaultStrokeColor = color;
        if (this.currentTool === this.tools.DRAW) {
            this.canvas.freeDrawingBrush.color = color;
        }
    }

    setStrokeWidth(width) {
        this.options.strokeWidth = width;
        if (this.currentTool === this.tools.DRAW) {
            this.canvas.freeDrawingBrush.width = width;
        }
    }
}

// Make available globally
window.agentDMSAnnotator = {
    create: (containerId, options) => new AgentDMSAnnotator(containerId, options)
};