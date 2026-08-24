(function () {
  var LAYERS = [
    { id: "population", label: "population", path: ["population"] },
    { id: "food_stock_kg", label: "stock alimentaire", path: ["food_stock_kg"] },
    { id: "food_deficit_kg", label: "déficit alimentaire", path: ["food_deficit_kg"] },
    { id: "hunger_ticks", label: "faim", path: ["hunger_ticks"] },
    { id: "insolation", label: "insolation", path: ["climate_drivers", "insolation_annual_mj_m2"] },
    { id: "dist_sea", label: "distance à la mer", path: ["climate_drivers", "dist_sea_centroid_m"] }
  ];

  var state = {
    snapshot: null,
    compare: null,
    layer: "population",
    scale: 1,
    ox: 0,
    oy: 0,
    bounds: null,
    selected: null,
    dragging: false,
    lastX: 0,
    lastY: 0
  };

  function readField(cell, path) {
    var value = cell;
    for (var i = 0; i < path.length; i += 1) {
      if (value === null || value === undefined) {
        return null;
      }
      value = value[path[i]];
    }
    return value === undefined ? null : value;
  }

  function classify(value) {
    if (value === null || value === undefined) {
      return "absent";
    }
    if (value === -1 || value === -1.0) {
      return "non_calcule";
    }
    if (value === 0 || value === 0.0) {
      return "zero";
    }
    return "valeur";
  }

  function rings(geometry) {
    var out = [];
    if (!geometry) {
      return out;
    }
    if (geometry.type === "Polygon") {
      out.push(geometry.coordinates[0]);
    } else if (geometry.type === "MultiPolygon") {
      geometry.coordinates.forEach(function (poly) {
        out.push(poly[0]);
      });
    }
    return out;
  }

  function computeBounds(cells) {
    var minx = Infinity;
    var miny = Infinity;
    var maxx = -Infinity;
    var maxy = -Infinity;
    cells.forEach(function (cell) {
      rings(cell.geometry).forEach(function (ring) {
        ring.forEach(function (pt) {
          minx = Math.min(minx, pt[0]);
          miny = Math.min(miny, pt[1]);
          maxx = Math.max(maxx, pt[0]);
          maxy = Math.max(maxy, pt[1]);
        });
      });
    });
    return { minx: minx, miny: miny, maxx: maxx, maxy: maxy };
  }

  function project(x, y, canvas) {
    var b = state.bounds;
    var spanX = Math.max(b.maxx - b.minx, 1);
    var spanY = Math.max(b.maxy - b.miny, 1);
    var usable = Math.min(canvas.width - 24, canvas.height - 24);
    var sx = usable / spanX;
    var sy = usable / spanY;
    var s = Math.min(sx, sy) * state.scale;
    var px = 12 + (x - b.minx) * s + state.ox;
    var py = 12 + (b.maxy - y) * s + state.oy;
    return { x: px, y: py };
  }

  function color(value, vmin, vmax) {
    var etat = classify(value);
    if (etat === "absent") {
      return "#9e9e9e";
    }
    if (etat === "non_calcule") {
      return "#6d4c41";
    }
    var t = 0;
    if (vmax > vmin) {
      t = (Number(value) - vmin) / (vmax - vmin);
    }
    if (t < 0) {
      t = 0;
    }
    if (t > 1) {
      t = 1;
    }
    var r = Math.round(8 + 247 * t);
    var g = Math.round(48 + 80 * (1 - t));
    var b = Math.round(107 + 40 * (1 - t));
    return "rgb(" + r + "," + g + "," + b + ")";
  }

  function currentLayer() {
    for (var i = 0; i < LAYERS.length; i += 1) {
      if (LAYERS[i].id === state.layer) {
        return LAYERS[i];
      }
    }
    return LAYERS[0];
  }

  function layerAvailable(doc, layer) {
    if (layer.id === "insolation" || layer.id === "dist_sea") {
      return doc.layers && doc.layers.climate_drivers_c1 &&
        doc.layers.climate_drivers_c1.status === "present";
    }
    return true;
  }

  function draw() {
    var canvas = document.getElementById("map");
    var ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    var layer = currentLayer();
    var values = state.snapshot.cells.map(function (cell) {
      return readField(cell, layer.path);
    });
    var numbers = values.filter(function (value) {
      return classify(value) === "valeur" || classify(value) === "zero";
    }).map(Number);
    var vmin = numbers.length ? Math.min.apply(null, numbers) : 0;
    var vmax = numbers.length ? Math.max.apply(null, numbers) : 1;
    state.snapshot.cells.forEach(function (cell) {
      var fill = color(readField(cell, layer.path), vmin, vmax);
      ctx.fillStyle = fill;
      ctx.strokeStyle = "#37474f";
      ctx.lineWidth = 0.4;
      rings(cell.geometry).forEach(function (ring) {
        ctx.beginPath();
        ring.forEach(function (pt, index) {
          var p = project(pt[0], pt[1], canvas);
          if (index === 0) {
            ctx.moveTo(p.x, p.y);
          } else {
            ctx.lineTo(p.x, p.y);
          }
        });
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
      });
    });
  }

  function showDetails(cell) {
    var box = document.getElementById("details");
    var hint = document.getElementById("hint");
    hint.textContent = "";
    box.innerHTML = "";
    Object.keys(cell).sort().forEach(function (key) {
      if (key === "geometry") {
        return;
      }
      var dt = document.createElement("dt");
      dt.textContent = key;
      var dd = document.createElement("dd");
      var value = cell[key];
      if (value === null) {
        dd.textContent = "absent";
      } else if (value === -1 || value === -1.0) {
        dd.textContent = "non calculé";
      } else if (typeof value === "object") {
        dd.textContent = JSON.stringify(value);
      } else {
        dd.textContent = String(value);
      }
      box.appendChild(dt);
      box.appendChild(dd);
    });
  }

  function hitTest(mx, my, canvas) {
    var cells = state.snapshot.cells;
    for (var i = 0; i < cells.length; i += 1) {
      var cell = cells[i];
      var inside = false;
      rings(cell.geometry).forEach(function (ring) {
        var ctx = canvas.getContext("2d");
        ctx.beginPath();
        ring.forEach(function (pt, index) {
          var p = project(pt[0], pt[1], canvas);
          if (index === 0) {
            ctx.moveTo(p.x, p.y);
          } else {
            ctx.lineTo(p.x, p.y);
          }
        });
        ctx.closePath();
        if (ctx.isPointInPath(mx, my)) {
          inside = true;
        }
      });
      if (inside) {
        return cell;
      }
    }
    return null;
  }

  function fillLayers() {
    var select = document.getElementById("layer");
    select.innerHTML = "";
    var unavailable = [];
    LAYERS.forEach(function (layer) {
      if (!layerAvailable(state.snapshot, layer)) {
        unavailable.push(layer.label);
        return;
      }
      var option = document.createElement("option");
      option.value = layer.id;
      option.textContent = layer.label;
      select.appendChild(option);
    });
    var notes = [];
    if (unavailable.length) {
      notes.push(unavailable.join(", ") + " indisponible");
    }
    document.getElementById("unavailable").textContent = notes.join(" · ");
    select.value = state.layer;
    select.addEventListener("change", function () {
      state.layer = select.value;
      draw();
    });
  }

  function bindMap() {
    var canvas = document.getElementById("map");
    canvas.addEventListener("wheel", function (event) {
      event.preventDefault();
      var factor = event.deltaY < 0 ? 1.1 : 0.9;
      state.scale *= factor;
      draw();
    }, { passive: false });
    canvas.addEventListener("mousedown", function (event) {
      state.dragging = true;
      state.lastX = event.offsetX;
      state.lastY = event.offsetY;
    });
    canvas.addEventListener("mouseup", function (event) {
      if (Math.abs(event.offsetX - state.lastX) < 3 &&
          Math.abs(event.offsetY - state.lastY) < 3) {
        var cell = hitTest(event.offsetX, event.offsetY, canvas);
        if (cell) {
          state.selected = cell;
          showDetails(cell);
        }
      }
      state.dragging = false;
    });
    canvas.addEventListener("mousemove", function (event) {
      if (!state.dragging) {
        return;
      }
      state.ox += event.offsetX - state.lastX;
      state.oy += event.offsetY - state.lastY;
      state.lastX = event.offsetX;
      state.lastY = event.offsetY;
      draw();
    });
  }

  function boot() {
    Promise.all([
      fetch("snapshot.json").then(function (res) { return res.json(); }),
      fetch("meta.json").then(function (res) { return res.json(); })
    ]).then(function (pair) {
      state.snapshot = pair[0];
      state.bounds = computeBounds(state.snapshot.cells);
      fillLayers();
      bindMap();
      draw();
      if (pair[1].has_compare) {
        return fetch("compare.json").then(function (res) { return res.json(); });
      }
      return null;
    }).then(function (compare) {
      state.compare = compare;
    });
  }

  boot();
}());
