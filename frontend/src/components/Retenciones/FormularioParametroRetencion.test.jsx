import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { FORMULARIO_PARAMETRO_VACIO } from "../../utils/parametrosRetencion";
import FormularioParametroRetencion from "./FormularioParametroRetencion";

function renderFormulario(cambios = {}) {
  const props = {
    form: { ...FORMULARIO_PARAMETRO_VACIO, ...cambios },
    onCambio: vi.fn(),
    cargando: false,
    onSubmit: vi.fn((e) => e.preventDefault()),
    onLimpiar: vi.fn(),
  };
  render(<FormularioParametroRetencion {...props} />);
  return props;
}

describe("FormularioParametroRetencion", () => {
  it("para ISR de proveedores ofrece tasa, tramo opcional, excedente, umbral y base del umbral", () => {
    renderFormulario();

    expect(screen.getByLabelText("Tasa base (%)")).toBeRequired();
    expect(screen.getByLabelText("Tramo límite (opcional)")).not.toBeRequired();
    expect(screen.getByLabelText("Tasa sobre el excedente (%)")).not.toBeRequired();
    expect(screen.getByLabelText("Umbral mínimo")).toBeInTheDocument();
    expect(screen.getByLabelText("Umbral sobre")).toHaveValue("BASE");
    expect(screen.queryByLabelText("Día límite del mes siguiente")).toBeNull();
  });

  it("la tasa sobre el excedente es obligatoria cuando se informa el tramo", () => {
    renderFormulario({ tramoLimite: "30000" });

    expect(screen.getByLabelText("Tasa sobre el excedente (%)")).toBeRequired();
  });

  it("para IVA retenido no ofrece tramo ni excedente", () => {
    renderFormulario({ concepto: "IVA_RETENIDO_CLIENTE" });

    expect(screen.getByLabelText("Tasa base (%)")).toBeInTheDocument();
    expect(screen.queryByLabelText("Tramo límite (opcional)")).toBeNull();
    expect(screen.queryByLabelText("Tasa sobre el excedente (%)")).toBeNull();
  });

  it("para el plazo de la constancia ofrece solo el día límite", () => {
    renderFormulario({ concepto: "PLAZO_CONSTANCIA" });

    expect(screen.getByLabelText("Día límite del mes siguiente")).toHaveAttribute("max", "28");
    expect(screen.queryByLabelText("Tasa base (%)")).toBeNull();
    expect(screen.queryByLabelText("Umbral mínimo")).toBeNull();
  });

  it("notifica el formulario completo al cambiar un campo y dispara limpiar y guardar", () => {
    const props = renderFormulario();

    fireEvent.change(screen.getByLabelText("Código"), { target: { value: "ISR_PROVEEDOR_ROS" } });
    fireEvent.change(screen.getByLabelText("Vigente desde"), { target: { value: "2027-01-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Limpiar" }));

    expect(props.onCambio).toHaveBeenNthCalledWith(1, { ...props.form, codigo: "ISR_PROVEEDOR_ROS" });
    expect(props.onCambio).toHaveBeenNthCalledWith(2, { ...props.form, vigenteDesde: "2027-01-01" });
    expect(props.onLimpiar).toHaveBeenCalledTimes(1);
  });

  it("para ISR a no residentes ofrece tasa y oculta umbral mínimo, umbral sobre y tramo", () => {
    renderFormulario({ concepto: "ISR_NO_RESIDENTE" });

    expect(screen.getByLabelText("Tasa base (%)")).toBeRequired();
    expect(screen.queryByLabelText("Tramo límite (opcional)")).toBeNull();
    expect(screen.queryByLabelText("Umbral mínimo")).toBeNull();
    expect(screen.queryByLabelText("Umbral sobre")).toBeNull();
  });
});
